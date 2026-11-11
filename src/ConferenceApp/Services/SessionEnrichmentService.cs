using System.Runtime.CompilerServices;
using ConferenceApp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
#if IOS || MACCATALYST
using Microsoft.Maui.Essentials.AI;
#endif

namespace ConferenceApp.Services;

public sealed class SessionEnrichmentService(
    ConferenceService conferenceService,
    IEmbeddingGenerator<string, Embedding<float>> generator,
    ILogger<SessionEnrichmentService> logger
#if IOS || MACCATALYST
    , ILoggerFactory loggerFactory
#endif
    )
{
    private readonly object gate = new();
    private Task? work;
    public event EventHandler? TopicsReady;

    public void Start()
    {
        lock (gate)
            work ??= Task.Run(EnrichAsync);
    }

    private async Task EnrichAsync()
    {
        try
        {
            var conference = await conferenceService.GetConferenceAsync();
            var sessions = conference.Sessions.Where(s => s.Kind == "session").ToArray();
            var directory = Path.Combine(FileSystem.AppDataDirectory, "enrichment", "sessions");
            Directory.CreateDirectory(directory);
            var files = new Dictionary<string, Session>();
            foreach (var session in sessions)
            {
                var path = Path.Combine(directory, Path.GetFileName(session.Content));
                using var source = await FileSystem.OpenAppPackageFileAsync($"conference/{session.Content}");
                using var destination = File.Create(path);
                await source.CopyToAsync(destination);
                files.Add(path, session);
            }

            try
            {
                await RunPipelineAsync(files, new TopicProcessor(generator), "topic",
                    (session, value) => session.Topic = value);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to classify all sessions by topic.");
            }
            await MainThread.InvokeOnMainThreadAsync(() => TopicsReady?.Invoke(this, EventArgs.Empty));

#if IOS || MACCATALYST
            if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
            {
#pragma warning disable MAUIAI0001
                using var chat = new AppleIntelligenceChatClient();
                var status = await AppleIntelligenceChatClient.GetModelStatusAsync();
                if (status.Availability == ChatModelAvailability.Available)
                {
                    var enricher = new SummaryEnricher(new EnricherOptions(chat)
                    {
                        BatchSize = 1,
                        LoggerFactory = loggerFactory
                    }, maxWordCount: 25);
                    await RunPipelineAsync(files, enricher, SummaryEnricher.MetadataKey,
                        (session, value) => session.Summary = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
                }
                else
                    logger.LogInformation("On-device summaries unavailable: {Availability}", status.Availability);
#pragma warning restore MAUIAI0001
            }
#endif
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Session enrichment could not be completed.");
        }
    }

    private async Task RunPipelineAsync(Dictionary<string, Session> files, IngestionChunkProcessor<string> processor,
        string metadataKey, Action<Session, string> update)
    {
        using var pipeline = new IngestionPipeline<string>(new MarkdownReader(), new WholeSessionChunker(files),
            new EnrichmentWriter(files, metadataKey, update));
        pipeline.ChunkProcessors.Add(processor);
        await foreach (var result in pipeline.ProcessAsync(files.Keys.Select(path => new FileInfo(path))))
        {
            if (!result.Succeeded)
                logger.LogWarning(result.Exception, "Could not enrich session {DocumentId}", result.DocumentId);
        }
    }

    private sealed class WholeSessionChunker(IReadOnlyDictionary<string, Session> sessions) : IngestionChunker<string>
    {
        public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
            IngestionDocument document, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elements = document.EnumerateContent().ToList();
            if (elements.FirstOrDefault() is IngestionDocumentHeader)
                elements.RemoveAt(0);
            var text = string.Join(" ", elements.Select(element => element.Text ?? element.GetMarkdown()));
            yield return new IngestionChunk<string>($"{sessions[document.Identifier].Title}. {text}", document);
            await Task.CompletedTask;
        }
    }

    private sealed class EnrichmentWriter(IReadOnlyDictionary<string, Session> sessions, string key,
        Action<Session, string> update) : IngestionChunkWriter<string>
    {
        public override async Task WriteAsync(IAsyncEnumerable<IngestionChunk<string>> chunks, CancellationToken cancellationToken = default)
        {
            await foreach (var chunk in chunks.WithCancellation(cancellationToken))
            {
                if (chunk.Metadata.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text))
                    await MainThread.InvokeOnMainThreadAsync(() => update(sessions[chunk.Document.Identifier], text));
            }
        }
    }
}
