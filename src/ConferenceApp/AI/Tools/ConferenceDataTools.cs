using System.ComponentModel;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceApp.AI;

// Source-generated AOT-safe tool context exposing ConferenceDataTools as AITool instances.
[AIToolSource(typeof(ConferenceDataTools))]
public partial class ConferenceDataToolContext : AIToolContext;

// Read-only tools for searching and reading sessions, schedules, and speaker biographies.
public sealed class ConferenceDataTools(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    ILogger<ConferenceDataTools> logger)
{
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

    [ExportAIFunction("get_sponsors")]
    [Description("List the event sponsors and their sponsorship tiers for a city, or the current city if omitted.")]
    public async Task<string> GetSponsorsAsync(
        [Description("Optional city name ('Durban', 'Cape Town', 'Bloemfontein').")] string city = "",
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var currentCity = await preferences.GetSelectedCityAsync();
        var targetCity = db.Cities.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(city) && (
                c.Id.Equals(city.Trim(), StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(city.Trim(), StringComparison.OrdinalIgnoreCase)))
            ?? currentCity;

        logger.LogInformation("[AI Tool] get_sponsors(city: '{CityId}')", targetCity.Id);

        var sponsors = db.Sponsors
            .Where(s => s.CityIds.Contains(targetCity.Id))
            .Select(s => $"{s.Name} ({s.Tier}) — {s.Url}")
            .ToArray();

        return sponsors.Length > 0
            ? string.Join('\n', sponsors)
            : $"No sponsors listed for {targetCity.Name}.";
    }

    internal static string FormatSessions(City city, IEnumerable<Session> sessions)
    {
        var matches = sessions.ToArray();
        if (matches.Length == 0)
            return $"No matching sessions in {city.Name}.";

        var lines = matches.Select(session =>
        {
            var topics = session.Topics.Count > 0 ? $" [{string.Join(", ", session.Topics)}]" : "";
            var summary = !string.IsNullOrWhiteSpace(session.Summary) ? $" — {session.Summary}" : "";

            return $"{session.Time} | {session.Track ?? "All attendees"}{topics} | {session.Title} | {session.SpeakerNames}{summary}";
        });

        return string.Join('\n', lines);
    }
}
