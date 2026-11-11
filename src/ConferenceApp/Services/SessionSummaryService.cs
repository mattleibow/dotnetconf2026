using System.Runtime.CompilerServices;
using ConferenceApp.Models;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
#if IOS || MACCATALYST
using Microsoft.Maui.Essentials.AI;
#endif

namespace ConferenceApp.Services;

#if IOS || MACCATALYST
public sealed class SessionSummaryService(ILoggerFactory loggerFactory, ILogger<SessionSummaryService> logger)
#else
public sealed class SessionSummaryService
#endif
{
    public string Status { get; private set; } = "Checking on-device summaries...";
    public event EventHandler? StatusChanged;

    public async Task GenerateAsync(IReadOnlyDictionary<string, Session> documents)
    {
#if IOS || MACCATALYST
        if (!(OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26)))
        {
            SetStatus("On-device summaries require iOS or Mac Catalyst 26 or later.");
            return;
        }
#pragma warning disable MAUIAI0001
        using var client = new AppleIntelligenceChatClient();
        try
        {
            var status = await AppleIntelligenceChatClient.GetModelStatusAsync();
            if (status.Availability != ChatModelAvailability.Available)
            {
                SetStatus($"On-device summaries unavailable: {status.Availability}.");
                return;
            }
            SetStatus("Generating summaries on device in the background...");
            var enricher = new SummaryEnricher(new EnricherOptions(client) { BatchSize = 1, LoggerFactory = loggerFactory }, 25);
            var writer = new SummaryWriter(documents, logger);
            using var pipeline = new IngestionPipeline<string>(new MarkdownReader(), new FullSessionChunker(), writer);
            pipeline.ChunkProcessors.Add(enricher);
            foreach (var path in documents.Keys)
            {
                await foreach (var result in pipeline.ProcessAsync([new FileInfo(path)]))
                    if (!result.Succeeded)
                    {
                        logger.LogWarning(result.Exception, "Could not summarize {Document}", path);
                        writer.MarkUnavailable(path);
                    }
            }
            SetStatus(writer.FailureCount == 0
                ? "Summaries generated on device."
                : $"Some AI summaries couldn't be generated ({writer.FailureCount} sessions).");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "On-device summaries failed.");
            SetStatus("On-device summaries are unavailable.");
        }
#pragma warning restore MAUIAI0001
#else
        await Task.CompletedTask;
        SetStatus("On-device summaries are not available on this platform.");
#endif
    }

    private void SetStatus(string status)
    {
        Status = status;
        MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke(this, EventArgs.Empty));
    }

    private sealed class FullSessionChunker : IngestionChunker<string>
    {
        public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
            IngestionDocument document, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var content = await File.ReadAllTextAsync(document.Identifier, cancellationToken);
            if (!string.IsNullOrWhiteSpace(content))
                yield return new IngestionChunk<string>(content, document, content);
        }
    }

    private sealed class SummaryWriter(IReadOnlyDictionary<string, Session> documents, ILogger logger) : IngestionChunkWriter<string>
    {
        private readonly HashSet<string> failures = [];
        public int FailureCount => failures.Count;

        public void MarkUnavailable(string documentId)
        {
            failures.Add(documentId);
            var session = documents[documentId];
            MainThread.BeginInvokeOnMainThread(() => session.SummaryUnavailable = true);
        }

        public override async Task WriteAsync(IAsyncEnumerable<IngestionChunk<string>> chunks, CancellationToken cancellationToken = default)
        {
            await foreach (var chunk in chunks.WithCancellation(cancellationToken))
            {
                if (chunk.Metadata.TryGetValue(SummaryEnricher.MetadataKey, out var value) && value is string summary && !string.IsNullOrWhiteSpace(summary))
                {
                    var session = documents[chunk.Document.Identifier];
                    var line = string.Join(" ", summary.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                    MainThread.BeginInvokeOnMainThread(() => session.Summary = line);
                }
                else
                {
                    logger.LogWarning("No summary was generated for {Document}", chunk.Document.Identifier);
                    MarkUnavailable(chunk.Document.Identifier);
                }
            }
        }
    }
}
