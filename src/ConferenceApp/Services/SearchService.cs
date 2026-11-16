using ConferenceApp.Data;

namespace ConferenceApp.Services;

public class SearchService(ConferenceDbProvider dbProvider)
{
    public async Task<IReadOnlyList<Session>> SearchSessionsAsync(string cityId, string query)
    {
        var db = await dbProvider.GetDbAsync();
        var sessions = db.Sessions.Where(s => s.CityId == cityId).ToList();
        var trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return sessions;

        return sessions
            .Where(session =>
                session.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                (session.Description?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
                session.Speakers.Any(s => s.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
