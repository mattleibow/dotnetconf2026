using ConferenceApp.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.Services;

public class SearchService(
    ConferenceDbProvider dbProvider,
    VectorStoreCollection<string, Session> sessionVectors,
    ILogger<SearchService> logger)
{
    private const int SemanticResultLimit = 5;

    public async Task<IReadOnlyList<Session>> SearchSessionsAsync(string cityId, string query)
    {
        var db = await dbProvider.GetDatabaseAsync();
        var sessions = db.Sessions.Where(s => s.CityId == cityId).ToList();
        var trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return sessions;

        var results = sessions
            .Where(session =>
                session.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                session.Speakers.Any(s => s.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        try
        {
            if (!await sessionVectors.CollectionExistsAsync())
                return results;

            var seen = results.Select(session => session.Id).ToHashSet();
            await foreach (var match in sessionVectors.SearchAsync(
                trimmed,
                top: SemanticResultLimit + results.Count,
                new() { Filter = s => s.CityId == cityId }))
            {
                if (seen.Add(match.Record.Id))
                    results.Add(match.Record);

                if (results.Count >= SemanticResultLimit)
                    break;
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
