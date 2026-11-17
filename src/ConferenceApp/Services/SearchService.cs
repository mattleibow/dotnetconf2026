using ConferenceApp.Data;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.Services;

// Searches conference sessions for the selected city.
public class SearchService(
    ConferenceDbProvider dbProvider,
    ILogger<SearchService> logger)
{
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

        return results;
    }
}
