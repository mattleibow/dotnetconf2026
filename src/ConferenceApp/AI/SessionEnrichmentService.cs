using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Models;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public record SessionsEnrichedMessage;

public sealed class SessionEnrichmentService(
    ConferenceDbProvider dbProvider,
    VectorStoreCollection<string, Session> sessionVectors,
    ILoggerFactory loggerFactory,
    ILogger<SessionEnrichmentService> logger,
    IChatClient? client = null)
{
    private static readonly string[] Topics = ["AI", "Mobile", "Web", "Cloud", "Data", "IoT"];

    private readonly TaskCompletionSource searchReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? processing;

    public Task StartAsync() => processing ??= Task.Run(EnrichAsync);

    public Task WaitForVectorIndexAsync()
    {
        _ = StartAsync();
        return searchReady.Task;
    }

    private async Task EnrichAsync()
    {
        var cachePath = Path.Combine(FileSystem.AppDataDirectory, "session-enrichment.json");

        try
        {
            var db = await dbProvider.GetDatabaseAsync();
            var sessions = db.Sessions.Where(s => s.IsTalk).ToList();

            // 1. Index the Session models into the VectorStoreCollection (automatically embeds Session.EmbeddingText).
            await sessionVectors.EnsureCollectionExistsAsync();
            foreach (var session in sessions)
                await sessionVectors.UpsertAsync(session);

            logger.LogInformation("[AI] Indexed {Count} session abstracts into VectorStoreCollection<string, Session>.", sessions.Count);
            searchReady.TrySetResult();

            // 2. Seed and load cached summaries and topics onto the in-memory Session models.
            if (!File.Exists(cachePath))
            {
                await using var packaged = await FileSystem.OpenAppPackageFileAsync("session-enrichment.json");
                await using var dest = File.Create(cachePath);
                await packaged.CopyToAsync(dest);
            }

            var cached = new Dictionary<string, CachedEnrichment>();
            if (File.Exists(cachePath))
            {
                await using var cache = File.OpenRead(cachePath);
                cached = await JsonSerializer.DeserializeAsync(cache,
                    EnrichmentJsonContext.Default.DictionaryStringCachedEnrichment) ?? [];

                foreach (var session in sessions)
                {
                    if (cached.TryGetValue(session.Id, out var entry))
                    {
                        session.Summary = entry.Summary;
                        session.Topic = entry.Topic != "None" ? entry.Topic : null;
                    }
                }

                logger.LogInformation("[AI] Loaded enrichment (summaries & topics) for {Count} sessions from local cache.", cached.Count);
                WeakReferenceMessenger.Default.Send(new SessionsEnrichedMessage());
            }

            // 3. Enrich any new/uncached talks in the background using MarkdownReader + SummaryEnricher + ClassificationEnricher.
            var remaining = sessions.Where(s => !cached.ContainsKey(s.Id)).ToList();
            if (remaining.Count == 0 || client is null)
                return;

            if (await ChatExtensions.GetModelAvailabilityAsync() != ChatModelAvailability.Available)
                return;

            var enricherOptions = new EnricherOptions(client)
            {
                BatchSize = 1,
                ChatOptions = new ChatOptions { Temperature = 0, MaxOutputTokens = 160 },
                LoggerFactory = loggerFactory
            };

            var summaryEnricher = new SummaryEnricher(enricherOptions, maxWordCount: 24);
            var classifier = new ClassificationEnricher(enricherOptions, Topics, fallbackClass: "None");

            foreach (var session in remaining)
            {
                logger.LogInformation("[AI] Enriching session {SessionId} with SummaryEnricher + ClassificationEnricher...", session.Id);
                var chunk = await ReadChunkAsync(session);

                await foreach (var enriched in classifier.ProcessAsync(summaryEnricher.ProcessAsync(SingleChunk(chunk))))
                {
                    var summary = ReadMetadata(enriched, SummaryEnricher.MetadataKey);
                    var topic = ReadMetadata(enriched, ClassificationEnricher.MetadataKey);
                    if (!Topics.Contains(topic, StringComparer.Ordinal))
                        topic = "None";

                    session.Summary = summary;
                    session.Topic = topic != "None" ? topic : null;
                    cached[session.Id] = new(summary, topic);
                }

                await using var cache = File.Create(cachePath);
                await JsonSerializer.SerializeAsync(cache, cached,
                    EnrichmentJsonContext.Default.DictionaryStringCachedEnrichment);
            }

            WeakReferenceMessenger.Default.Send(new SessionsEnrichedMessage());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[AI] Background session enrichment stopped.");
        }
        finally
        {
            searchReady.TrySetResult();
        }
    }

    private static string ReadMetadata(IngestionChunk<string> chunk, string key) =>
        chunk.Metadata.TryGetValue(key, out var value) && value is string text
            ? string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            : "";

    private static async Task<IngestionChunk<string>> ReadChunkAsync(Session session)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync($"conference/{session.Content}");
        var document = await new MarkdownReader().ReadAsync(stream, session.Id, "text/markdown");
        var text = string.Join("\n\n", document.EnumerateContent().Select(element => element.GetMarkdown()));
        return new(text, document);
    }

    private static async IAsyncEnumerable<IngestionChunk<string>> SingleChunk(IngestionChunk<string> chunk)
    {
        yield return chunk;
        await Task.CompletedTask;
    }
}

internal record CachedEnrichment(string Summary, string Topic);

[JsonSerializable(typeof(Dictionary<string, CachedEnrichment>))]
internal partial class EnrichmentJsonContext : JsonSerializerContext;
