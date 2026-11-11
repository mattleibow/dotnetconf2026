using CommunityToolkit.VectorData.InMemory;
using ConferenceApp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.Services;

public sealed class SemanticSessionSearch(ConferenceService data, IServiceProvider services, ILogger<SemanticSessionSearch> logger) : IDisposable
{
    private readonly Lock sync = new();
    private Task? initialization;
    private IngestionPipeline<string>? pipeline;
    private VectorStoreCollection<object, Dictionary<string, object?>>? collection;
    private Dictionary<string, Session> sessions = [];
    private int chunkCount;
    public bool IsAvailable { get; private set; }
    public bool IsInitialized { get; private set; }

    public Task InitializeAsync()
    {
        lock (sync)
            return initialization ??= Task.Run(BuildIndexAsync);
    }

    private async Task BuildIndexAsync()
    {
        var conference = await data.GetConferenceAsync();
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

        var store = new InMemoryVectorStore(new() { EmbeddingGenerator = generator });
        var writer = new VectorStoreWriter<string>(store, MauiProgram.EmbeddingDimensions,
            new() { CollectionName = "sessions", IncrementalIngestion = false });
        var chunker = new SessionChunker(documents);
        var ingestion = new IngestionPipeline<string>(new MarkdownReader(), chunker, writer);
        try
        {
            await foreach (var result in ingestion.ProcessAsync(documents.Keys.Select(path => new FileInfo(path))))
            {
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Failed to index session document '{result.DocumentId}'.", result.Exception);
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

    public async Task<IReadOnlyList<SessionSearchResult>> SearchAsync(string query, string cityId, IReadOnlySet<string> keywordIds)
    {
        await InitializeAsync();
        if (!IsAvailable || chunkCount == 0)
            return [];

        var matches = new List<(SessionSearchResult Result, double Score)>();
        await foreach (var result in collection!.SearchAsync(query, chunkCount))
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
