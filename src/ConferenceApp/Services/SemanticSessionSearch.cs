using System.Text.RegularExpressions;
using CommunityToolkit.VectorData.InMemory;
using ConferenceApp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.Services;

public sealed class SemanticSessionSearch(ConferenceService data, IServiceProvider services, ILogger<SemanticSessionSearch> logger)
{
    private readonly Lock sync = new();
    private Task? initialization;
    private VectorStoreCollection<string, SessionChunk>? collection;
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
        var store = new InMemoryVectorStore(new() { EmbeddingGenerator = generator });
        var index = store.GetCollection<string, SessionChunk>("sessions");
        await index.EnsureCollectionExistsAsync();
        var records = new List<SessionChunk>();
        foreach (var session in conference.Sessions.Where(s => s.Kind != "general"))
        {
            var content = await data.GetContentAsync(session.Content);
            var sentences = Regex.Split(Regex.Replace(content, @"\s+", " ").Trim(), @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            for (var i = 0; i < sentences.Length; i += 2)
            {
                var excerpt = string.Join(" ", sentences.Skip(i).Take(2));
                records.Add(new SessionChunk
                {
                    Key = $"{session.Id}:{i}",
                    SessionId = session.Id,
                    Text = $"{session.Title}. {excerpt}",
                    Excerpt = excerpt
                });
            }
        }

        await index.UpsertAsync(records);
        sessions = conference.Sessions.ToDictionary(s => s.Id);
        chunkCount = records.Count;
        collection = index;
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
            var session = sessions[result.Record.SessionId];
            if (session.CityId != cityId || result.Score is null || keywordIds.Contains(session.Id))
                continue;
            matches.Add((new SessionSearchResult(session, $"Semantic · {result.Record.Excerpt}", true), result.Score.Value));
        }
        return matches.OrderByDescending(m => m.Score)
            .DistinctBy(m => m.Result.Session.Id)
            .Take(5).Select(m => m.Result).ToList();
    }

    public sealed class SessionChunk
    {
        [VectorStoreKey] public string Key { get; set; } = "";
        [VectorStoreData] public string SessionId { get; set; } = "";
        [VectorStoreData] public string Excerpt { get; set; } = "";
        [VectorStoreVector(MauiProgram.EmbeddingDimensions)] public string Text { get; set; } = "";
    }
}
