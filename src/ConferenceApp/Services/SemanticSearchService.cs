#if IOS || MACCATALYST
using System.Text.RegularExpressions;
using CommunityToolkit.VectorData.InMemory;
using ConferenceApp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.Services;

public sealed class SemanticSearchService(
    ConferenceService conferenceService,
    IServiceProvider services,
    ILogger<SemanticSearchService> logger)
{
    private Task<Index>? initialization;

    public void Start() => initialization ??= Task.Run(BuildIndexAsync);

    public async Task<SessionSearchResponse> SearchAsync(string query, string cityId)
    {
        Start();
        try
        {
            var index = await initialization!;
            var results = new List<SessionSearchResult>();
            var seen = new HashSet<string>();
            if (index.Count == 0)
                return new(results, "");
            await foreach (var hit in index.Collection.SearchAsync(query, index.Count))
            {
                var session = index.Sessions[hit.Record.SessionId];
                if (session.CityId == cityId && seen.Add(session.Id))
                    results.Add(new SessionSearchResult(session, hit.Record.Excerpt, true));
            }
            return new(results, "");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Semantic search unavailable; using keyword search.");
            return new([], "Semantic search unavailable. Showing keyword matches only.");
        }
    }

    private async Task<Index> BuildIndexAsync()
    {
        var generator = services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var store = new InMemoryVectorStore(new() { EmbeddingGenerator = generator });
        var collection = store.GetCollection<int, SessionChunk>("sessions");
        await collection.EnsureCollectionExistsAsync();
        var data = await conferenceService.GetConferenceAsync();
        var sessions = data.Sessions.Where(s => s.Kind == "session").ToDictionary(s => s.Id);
        var count = 0;
        foreach (var session in sessions.Values)
        {
            var text = await conferenceService.GetContentAsync(session.Content);
            var normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var sentences = Regex.Split(normalized, @"(?<=[.!?])\s+").Where(s => s.Length > 0).ToArray();
            for (var i = 0; i < sentences.Length; i += 2)
            {
                var excerpt = string.Join(" ", sentences.Skip(i).Take(2));
                await collection.UpsertAsync(new SessionChunk
                {
                    Key = count++,
                    SessionId = session.Id,
                    Excerpt = excerpt,
                    Text = $"{session.Title}. {excerpt}"
                });
            }
        }
        return new(collection, sessions, count);
    }

    private sealed record Index(InMemoryCollection<int, SessionChunk> Collection, Dictionary<string, Session> Sessions, int Count);

    private sealed class SessionChunk
    {
        [VectorStoreKey]
        public int Key { get; set; }
        [VectorStoreData]
        public string SessionId { get; set; } = "";
        [VectorStoreData]
        public string Excerpt { get; set; } = "";
        [VectorStoreVector(512)]
        public string Text { get; set; } = "";
    }
}
#endif
