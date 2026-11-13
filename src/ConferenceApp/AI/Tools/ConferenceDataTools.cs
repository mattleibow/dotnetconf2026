using System.ComponentModel;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceApp.AI;

[AIToolSource(typeof(ConferenceDataTools))]
public partial class ConferenceDataToolContext : AIToolContext;

/// <summary>
/// Read-only tools for searching and reading sessions, schedules, and speaker biographies.
/// </summary>
public sealed class ConferenceDataTools(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    ILogger<ConferenceDataTools> logger)
{
    public string? LastListedSessions { get; private set; }

    [ExportAIFunction("search_sessions")]
    [Description("Search sessions by topic, title, or speaker in the current city. Returns summaries; call get_session for the full abstract.")]
    public async Task<string> SearchSessionsAsync(
        [Description("Topic, title, or speaker to search for.")] string query,
        [FromServices] SearchService search,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync();
        logger.LogInformation("[AI Tool] search_sessions(query: '{Query}') in {CityId}", query, city.Id);

        var sessions = await search.SearchSessionsAsync(city.Id, query);
        return FormatSessions(city, sessions.Take(5));
    }

    [ExportAIFunction("get_session")]
    [Description("Read the full abstract and schedule details for a specific session in the current city.")]
    public async Task<string> GetSessionAsync(
        [Description("Session ID or title.")] string session,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var match = db.FindSession(city.Id, session);

        if (match is null)
            return $"No session matching '{session}' in {city.Name}.";

        logger.LogInformation("[AI Tool] get_session('{SessionId}')", match.Id);

        var summary = FormatSessions(city, [match]);
        return $"{summary}\nAbstract: {match.Description}";
    }

    [ExportAIFunction("get_speaker")]
    [Description("Read a speaker's tagline and full biography in the current city.")]
    public async Task<string> GetSpeakerAsync(
        [Description("Speaker ID or name.")] string speaker,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var queries = speaker.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matches = db.Speakers
            .Where(s => s.CityIds.Contains(city.Id) && queries.Any(q =>
                s.Id.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                q.Contains(s.Name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (matches.Length == 0)
            return $"No speaker matching '{speaker}' in {city.Name}.";

        var results = new List<string>(matches.Length);
        foreach (var match in matches)
        {
            logger.LogInformation("[AI Tool] get_speaker('{SpeakerId}')", match.Id);
            results.Add($"Name: {match.Name} | {match.Tagline}\nBiography: {match.Bio}");
        }

        return string.Join("\n\n", results);
    }

    [ExportAIFunction("list_sessions")]
    [Description("List candidate talks in the current city between two 24-hour daytime times (for example 08:00 to 17:00), excluding time slots already booked in the user's favourites.")]
    public async Task<string> ListSessionsAsync(
        [Description("Start time in 24-hour HH:mm format (default 08:00).")] TimeOnly start,
        [Description("End time in 24-hour HH:mm format (default 17:00).")] TimeOnly end,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        logger.LogInformation("[AI Tool] list_sessions(start: {Start}, end: {End}) in {CityId}", start, end, city.Id);

        var bookedTimes = db.Sessions
            .Where(s => s.CityId == city.Id && s.IsTalk && preferences.IsFavorite(s.Id))
            .Select(s => s.Time)
            .ToHashSet(StringComparer.Ordinal);

        var matching = db.Sessions
            .Where(s =>
                s.CityId == city.Id &&
                s.IsTalk &&
                !bookedTimes.Contains(s.Time) &&
                TimeOnly.FromDateTime(s.Start.DateTime) >= start &&
                TimeOnly.FromDateTime(s.Start.DateTime) < end)
            .ToArray();

        if (matching.Length == 0)
            return $"No open sessions in {city.Name} between {start:HH:mm} and {end:HH:mm}.";

        var lines = matching.Select(s =>
        {
            var topic = !string.IsNullOrWhiteSpace(s.Topic) ? $" [{s.Topic}]" : "";
            return $"{s.Time} | {s.Track ?? "All attendees"}{topic} | {s.Title} ({s.SpeakerNames})";
        });

        return LastListedSessions = string.Join('\n', lines);
    }

    internal static string FormatSessions(City city, IEnumerable<Session> sessions)
    {
        var matches = sessions.ToArray();
        if (matches.Length == 0)
            return $"No matching sessions in {city.Name}.";

        var lines = matches.Select(session =>
        {
            var topic = !string.IsNullOrWhiteSpace(session.Topic) ? $" [{session.Topic}]" : "";
            var summary = !string.IsNullOrWhiteSpace(session.Summary) ? $" — {session.Summary}" : "";

            return $"{session.Time} | {session.Track ?? "All attendees"}{topic} | {session.Title} | {session.SpeakerNames}{summary}";
        });

        return string.Join('\n', lines);
    }
}
