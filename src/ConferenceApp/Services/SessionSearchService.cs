using ConferenceApp.Models;

namespace ConferenceApp.Services;

public sealed class SessionSearchService(ConferenceService conferenceService)
{
    public async Task<IReadOnlyList<SessionSearchResult>> SearchSessionsAsync(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
            return [];

        var cityId = conferenceService.CityId;
        var data = await conferenceService.GetConferenceAsync();
        var results = await Task.WhenAll(data.Sessions.Where(s => s.CityId == cityId).Select(async session =>
        {
            var fields = new[] { (Name: "Title", Text: session.Title), (Name: "Track", Text: session.Track ?? "") }
                .Concat(data.Speakers.Where(s => session.SpeakerIds.Contains(s.Id)).Select(s => (Name: "Speaker", Text: s.Name)));
            foreach (var field in fields)
            {
                var match = field.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                if (match >= 0)
                    return new SessionSearchResult(session, $"{field.Name}: {field.Text.Substring(match, query.Length)}");
            }

            var abstractText = await conferenceService.GetContentAsync(session.Content);
            var normalized = string.Join(" ", abstractText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var index = normalized.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            var start = Math.Max(0, index - 60);
            var end = Math.Min(normalized.Length, index + query.Length + 60);
            var excerpt = (start > 0 ? "…" : "") + normalized[start..end] + (end < normalized.Length ? "…" : "");
            return new SessionSearchResult(session, excerpt);
        }));
        return results.OfType<SessionSearchResult>().ToList();
    }
}

public sealed record SessionSearchResult(Session Session, string MatchText);
