using System.ComponentModel;
using ConferenceApp.Models;
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
    ConferenceDatabase database,
    UserPreferencesService preferences,
    ILogger<ConferenceDataTools> logger)
{
    [ExportAIFunction("search_sessions")]
    [Description("Search sessions by topic, title, or speaker in the current city. Returns summaries; call get_session for the full abstract.")]
    public async Task<string> SearchSessionsAsync(
        [Description("Topic, title, or speaker to search for.")] string query,
        [FromServices] SearchService search,
        [FromServices] SessionEnrichmentService enrichment,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        logger.LogInformation("[AI Tool] search_sessions(query: '{Query}') in {CityId}", query, city.Id);

        var sessions = await search.SearchSessionsAsync(city.Id, query);
        return await FormatSessionsAsync(city, sessions.Take(5), enrichment);
    }

    [ExportAIFunction("get_session")]
    [Description("Read the full abstract and schedule details for a specific session in the current city.")]
    public async Task<string> GetSessionAsync(
        [Description("Session ID or title.")] string session,
        [FromServices] SessionEnrichmentService enrichment,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        var sessions = await database.GetSessionsAsync(city.Id);
        var match = sessions.FirstOrDefault(s =>
            s.Id.Equals(session, StringComparison.OrdinalIgnoreCase) ||
            s.Title.Contains(session, StringComparison.OrdinalIgnoreCase) ||
            session.Contains(s.Title, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return $"No session matching '{session}' in {city.Name}.";

        logger.LogInformation("[AI Tool] get_session('{SessionId}')", match.Id);

        var summary = await FormatSessionsAsync(city, [match], enrichment);
        var abstractText = await database.GetTextAsync(match.Content);
        return $"{summary}\nAbstract: {abstractText}";
    }

    [ExportAIFunction("get_speaker")]
    [Description("Read a speaker's tagline and full biography in the current city.")]
    public async Task<string> GetSpeakerAsync(
        [Description("Speaker ID or name.")] string speaker,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        var speakers = await database.GetSpeakersAsync(city.Id);
        var queries = speaker.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var matches = speakers.Where(s => queries.Any(q =>
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
            var bio = await database.GetTextAsync(match.Content);
            results.Add($"Name: {match.Name} | {match.Tagline}\nBiography: {bio}");
        }

        return string.Join("\n\n", results);
    }

    [ExportAIFunction("list_sessions")]
    [Description("List sessions in the current city between two 24-hour daytime times (for example 08:00 to 12:00).")]
    public async Task<string> ListSessionsAsync(
        [Description("Start time in 24-hour HH:mm format (e.g. 08:00).")] TimeOnly start,
        [Description("End time in 24-hour HH:mm format (e.g. 12:00).")] TimeOnly end,
        [FromServices] SessionEnrichmentService enrichment,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        logger.LogInformation("[AI Tool] list_sessions(start: {Start}, end: {End}) in {CityId}", start, end, city.Id);

        var sessions = await database.GetSessionsAsync(city.Id);
        var matching = sessions.Where(s =>
            s.IsTalk &&
            TimeOnly.FromDateTime(s.Start.DateTime) >= start &&
            TimeOnly.FromDateTime(s.Start.DateTime) < end);

        return await FormatSessionsAsync(city, matching, enrichment);
    }

    internal async Task<string> FormatSessionsAsync(
        City city,
        IEnumerable<Session> sessions,
        SessionEnrichmentService enrichment)
    {
        var matches = sessions.ToArray();
        if (matches.Length == 0)
            return $"No matching sessions in {city.Name}.";

        var conference = await database.GetConferenceAsync();
        var lines = matches.Select(session =>
        {
            var speakers = string.Join(", ", session.SpeakerIds.Select(id =>
                conference.Speakers.First(s => s.Id == id).Name));
            var enriched = enrichment.GetSession(session.Id);
            var topic = enriched.HasTopic ? $" [{enriched.Topic}]" : "";
            var summary = !string.IsNullOrWhiteSpace(enriched.Summary) ? $" — {enriched.Summary}" : "";

            return $"{session.Time} | {session.Track ?? "All attendees"}{topic} | {session.Title} | {speakers}{summary}";
        });

        return string.Join('\n', lines);
    }
}
