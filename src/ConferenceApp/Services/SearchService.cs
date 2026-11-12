using ConferenceApp.AI;
using ConferenceApp.Models;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.Services;

public class SearchService(
    ConferenceDatabase database,
    SessionEnrichmentService enrichment,
    ILogger<SearchService> logger)
{
    private const int SemanticResultLimit = 5;

    public async Task<IReadOnlyList<Session>> SearchSessionsAsync(string cityId, string query)
    {
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
            var collection = await enrichment.GetSearchCollectionAsync();
            if (collection is null)
                return results;

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
            foreach (var (id, _) in matches.Where(m => !seen.Contains(m.Id)).Take(SemanticResultLimit))
            {
                if (byId.TryGetValue(id, out var session) && seen.Add(id))
                    results.Add(session);
            }
            logger.LogInformation("[AI Search] Query '{Query}' in {CityId} -> Top match: '{TopTitle}' ({Total} results)",
                trimmed, cityId, results.FirstOrDefault()?.Title ?? "none", results.Count);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            logger.LogError(exception, "[AI Search] On-device session search failed for city {CityId}.", cityId);
        }
        return results;
    }
}
