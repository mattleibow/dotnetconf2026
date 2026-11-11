using ConferenceApp.Models;

namespace ConferenceApp.Services;

public sealed class SessionSearchService(ConferenceService data, CityService city)
{
    public async Task<IReadOnlyList<SessionSearchResult>> SearchSessionsAsync(string? query)
    {
        var term = query?.Trim();
        if (string.IsNullOrEmpty(term))
            return [];

        var cityId = city.SelectedId;
        var conference = await data.GetConferenceAsync();
        var sessions = conference.Sessions.Where(s => s.CityId == cityId);
        var results = await Task.WhenAll(sessions.Select(async session =>
        {
            var speakers = conference.Speakers.Where(s => session.SpeakerIds.Contains(s.Id));
            var fields = new[] { ("Title", (string?)session.Title), ("Track", session.Track) }
                .Concat(speakers.Select(s => ("Speaker", (string?)s.Name)));
            foreach (var (label, value) in fields)
            {
                if (value is not null && Excerpt(value, term) is { } excerpt)
                    return new SessionSearchResult(session, $"{label}: {excerpt}");
            }

            var content = await data.GetContentAsync(session.Content);
            return Excerpt(content, term) is { } abstractMatch
                ? new SessionSearchResult(session, $"Abstract: {abstractMatch}")
                : null;
        }));
        return results.OfType<SessionSearchResult>().ToList();
    }

    private static string? Excerpt(string text, string term)
    {
        var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        var start = Math.Max(0, index - 30);
        var end = Math.Min(text.Length, index + term.Length + 45);
        return $"{(start > 0 ? "…" : "")}{text[start..end].Replace('\n', ' ').Replace('\r', ' ')}{(end < text.Length ? "…" : "")}";
    }
}

public sealed record SessionSearchResult(Session Session, string Match);
