using CommunityToolkit.VectorData.InMemory;
using ConferenceApp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.Services;

public sealed class SemanticSessionSearch(ConferenceService data, IServiceProvider services, SessionSummaryService summaries, ILogger<SemanticSessionSearch> logger) : IDisposable
{
    private readonly Lock sync = new();
    private Task? initialization;
    private IngestionPipeline<string>? pipeline;
    private VectorStoreCollection<object, Dictionary<string, object?>>? collection;
    private Dictionary<string, Session> sessions = [];
    private int chunkCount;
    public bool IsAvailable { get; private set; }
    public bool IsInitialized { get; private set; }
    public event EventHandler? TopicsChanged;

    public Task InitializeAsync()
    {
        lock (sync)
            return initialization ??= Task.Run(BuildIndexAsync);
    }

    private async Task BuildIndexAsync()
    {
        var conference = await data.GetConferenceAsync();
        var directory = Path.Combine(FileSystem.AppDataDirectory, "session-search");
        Directory.CreateDirectory(directory);
        var documents = new Dictionary<string, Session>();
        foreach (var session in conference.Sessions.Where(s => s.Kind != "general"))
        {
            var path = Path.Combine(directory, Path.GetFileName(session.Content));
            using var source = await FileSystem.OpenAppPackageFileAsync($"conference/{session.Content}");
            using var destination = File.Create(path);
            await source.CopyToAsync(destination);
            documents.Add(path, session);
        }

        _ = Task.Run(() => summaries.GenerateAsync(documents));
        IEmbeddingGenerator<string, Embedding<float>> generator;
        try
        {
            generator = services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning(ex, "Sentence embeddings are unavailable; using keyword search.");
            IsInitialized = true;
            return;
        }
        logger.LogInformation("Semantic search using {Generator}", generator.GetType().Name);
        var store = new InMemoryVectorStore(new() { EmbeddingGenerator = generator });
        var dimensions = generator is OnnxEmbeddingGenerator ? 384 : 512;
        var writer = new VectorStoreWriter<string>(store, dimensions,
            new() { CollectionName = "sessions", IncrementalIngestion = false });
        var chunker = new SessionChunker(documents);
        var ingestion = new IngestionPipeline<string>(new MarkdownReader(), chunker, writer);
        var topics = new TopicProcessor(generator);
        ingestion.ChunkProcessors.Add(topics);
        try
        {
            await foreach (var result in ingestion.ProcessAsync(documents.Keys.Select(path => new FileInfo(path))))
            {
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Failed to index session document '{result.DocumentId}'.", result.Exception);
                var session = documents[result.DocumentId];
                var topic = topics.TopicFor(result.DocumentId);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    session.Topic = topic;
                    TopicsChanged?.Invoke(this, EventArgs.Empty);
                });
            }
        }
        catch
        {
            ingestion.Dispose();
            throw;
        }

        sessions = documents;
        chunkCount = chunker.ChunkCount;
        collection = chunkCount > 0 ? writer.VectorStoreCollection : null;
        pipeline = ingestion;
        IsAvailable = true;
        IsInitialized = true;
    }

    public async Task<IReadOnlyList<SessionSearchResult>> SearchAsync(string query, string cityId, IReadOnlySet<string> keywordIds, CancellationToken cancellationToken = default)
    {
        await InitializeAsync().WaitAsync(cancellationToken);
        if (!IsAvailable || chunkCount == 0)
            return [];

        var matches = new List<(SessionSearchResult Result, double Score)>();
        await foreach (var result in collection!.SearchAsync(query, chunkCount, cancellationToken: cancellationToken))
        {
            var session = sessions[(string)result.Record["documentid"]!];
            if (session.CityId != cityId || result.Score is null || keywordIds.Contains(session.Id))
                continue;
            matches.Add((new SessionSearchResult(session, $"Semantic · {result.Record["context"]}", true), result.Score.Value));
        }
        return matches.OrderByDescending(m => m.Score)
            .DistinctBy(m => m.Result.Session.Id)
            .Take(5).Select(m => m.Result).ToList();
    }

    public void Dispose() => pipeline?.Dispose();
}
