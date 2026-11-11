using ConferenceApp.Models;

namespace ConferenceApp.Services;

public class SearchService(ConferenceDatabase database)
{
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
        return results;
    }
}
