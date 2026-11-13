using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.VectorData.InMemory;
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
    ConferenceDatabase database,
    ILoggerFactory loggerFactory,
    ILogger<SessionEnrichmentService> logger,
    IChatClient? client = null,
    IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null) : IDisposable
{
    private static readonly string[] Topics = ["AI", "Mobile", "Web", "Cloud", "Data", "IoT"];
    private const int VectorDimensions = 384;

    private readonly TaskCompletionSource<VectorStoreCollection<object, Dictionary<string, object?>>?> searchReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private VectorStoreWriter<string>? vectorWriter;
    private Task? processing;

    public Task StartAsync() => processing ??= Task.Run(EnrichAsync);

    public Task<VectorStoreCollection<object, Dictionary<string, object?>>?> GetSearchCollectionAsync()
    {
        _ = StartAsync();
        return searchReady.Task;
    }

    private async Task EnrichAsync()
    {
        var cachePath = Path.Combine(FileSystem.AppDataDirectory, "session-enrichment.json");

        try
        {
            var sessions = (await database.GetConferenceAsync()).Sessions.Where(s => s.IsTalk).ToList();
            var chunks = new Dictionary<string, IngestionChunk<string>>();

            // 1. Read session Markdown streams with MarkdownReader and index them into InMemoryVectorStore.
            if (embeddingGenerator is not null)
            {
                var store = new InMemoryVectorStore(new() { EmbeddingGenerator = embeddingGenerator });
                vectorWriter = new(store, VectorDimensions, new()
                {
                    CollectionName = "sessions",
                    DistanceFunction = DistanceFunction.CosineSimilarity
                });

                foreach (var session in sessions)
                {
                    var chunk = await ReadChunkAsync(session);
                    chunk.Metadata["cityid"] = session.CityId;
                    await vectorWriter.WriteAsync(SingleChunk(chunk));
                    chunks.Add(session.Id, chunk);
                }

                logger.LogInformation("[AI] Indexed {Count} session abstracts with {Dimensions}-dimensional embeddings via DataIngestion VectorStoreWriter.",
                    chunks.Count, VectorDimensions);
                searchReady.TrySetResult(vectorWriter.VectorStoreCollection);
            }
            else
            {
                searchReady.TrySetResult(null);
            }

            // 2. Seed and load cached summaries and topics onto the ConferenceDatabase Session models.
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

            // 3. Enrich any new/uncached talks in the background using SummaryEnricher + ClassificationEnricher.
            var remaining = sessions.Where(s => string.IsNullOrWhiteSpace(s.Summary)).ToList();
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
                var chunk = chunks.TryGetValue(session.Id, out var indexed) ? indexed : await ReadChunkAsync(session);

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
            searchReady.TrySetResult(null);
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

    public void Dispose()
    {
        vectorWriter?.Dispose();
        GC.SuppressFinalize(this);
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
