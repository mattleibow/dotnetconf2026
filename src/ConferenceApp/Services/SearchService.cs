using ConferenceApp.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.Services;

// Searches conference sessions for the selected city.
public class SearchService(
    ConferenceDbProvider dbProvider,
    VectorStoreCollection<string, Session> sessionVectors,
    ILogger<SearchService> logger)
{
    private const int SemanticResultLimit = 10;
    private const double MinimumSemanticScore = 0.3;

    public async Task<IReadOnlyList<Session>> SearchSessionsAsync(string cityId, string query)
    {
        var db = await dbProvider.GetDbAsync();
        var sessions = db.Sessions.Where(s => s.CityId == cityId).ToList();
        var trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return sessions;

        var results = sessions
            .Where(session =>
                session.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                (session.Description?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
                session.Speakers.Any(s => s.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        try
        {
            if (!await sessionVectors.CollectionExistsAsync())
                return results;

            // Append semantic vector matches from the in-memory Session collection filtered by CityId.
            await foreach (var match in sessionVectors.SearchAsync(
                trimmed,
                top: SemanticResultLimit,
                new() { Filter = s => s.CityId == cityId }))
            {
                if (match.Score >= MinimumSemanticScore && !results.Contains(match.Record))
                    results.Add(match.Record);
            }

            logger.LogInformation("[AI Search] Query '{Query}' in {CityId} -> Top match: '{TopTitle}' ({Total} results)",
                trimmed, cityId, results.FirstOrDefault()?.Title ?? "none", results.Count);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[AI Search] On-device session search failed for city {CityId}.", cityId);
        }

        return results;
    }
}
