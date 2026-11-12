using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.AI;
using ConferenceApp.Models;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.Services;

public partial class SearchService(ConferenceDatabase database, SessionEnrichmentService enrichment,
    ILogger<SearchService> logger) : ObservableObject
{
    private const int SemanticResultLimit = 5;
    private int searchVersion;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    public async Task<IReadOnlyList<Session>> SearchSessionsAsync(string cityId, string query)
    {
        var version = Interlocked.Increment(ref searchVersion);
        await SetStatusAsync("", version);
        var sessions = await database.GetSessionsAsync(cityId);
        var trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return sessions;

        var results = new List<Session>();
        foreach (var session in sessions)
        {
            var speakers = await database.GetSpeakersForSessionAsync(session);
            if (session.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                speakers.Any(s => s.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(session);
            }
        }

        try
        {
            var ready = enrichment.GetSearchCollectionAsync();
            if (!ready.IsCompleted)
                await SetStatusAsync("Preparing on-device search...", version);
            var collection = await ready;
            if (collection is null)
            {
                await SetStatusAsync("Semantic search isn't available on this device. Showing title and speaker matches.", version);
                return results;
            }

            var matches = await Task.Run(async () =>
            {
                var ids = new List<(string Id, double? Score)>();
                await foreach (var match in collection.SearchAsync(trimmed, top: SemanticResultLimit + results.Count,
                    options: new() { Filter = record => (string)record["cityid"]! == cityId }))
                {
                    ids.Add(((string)match.Record["documentid"]!, match.Score));
                }
                return ids;
            });
            var byId = sessions.ToDictionary(session => session.Id);
            var seen = results.Select(session => session.Id).ToHashSet();
            foreach (var (id, score) in matches.Where(m => !seen.Contains(m.Id)).Take(SemanticResultLimit))
            {
                if (byId.TryGetValue(id, out var session) && seen.Add(id))
                    results.Add(session);
            }
            logger.LogInformation("[AI Search] Query '{Query}' in {CityId} -> Top match: '{TopTitle}' ({Total} results)",
                trimmed, cityId, results.FirstOrDefault()?.Title ?? "none", results.Count);
            await SetStatusAsync("", version);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            logger.LogError(exception, "[AI Search] On-device session search failed for city {CityId}.", cityId);
            await SetStatusAsync("Couldn't search by meaning. Showing title and speaker matches.", version);
        }
        return results;
    }

    private Task SetStatusAsync(string message, int version) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (version == Volatile.Read(ref searchVersion))
                StatusMessage = message;
        });
}
