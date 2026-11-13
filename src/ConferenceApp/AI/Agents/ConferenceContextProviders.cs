using ConferenceApp.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

/// <summary>
/// Context Provider 1: Injects the user's current city, event date, current time, venue, and saved favourites.
/// </summary>
public sealed class UserContextProvider(IServiceProvider services) : AIContextProvider
{
    private readonly ConferenceDatabase database = services.GetRequiredService<ConferenceDatabase>();
    private readonly UserPreferencesService preferences = services.GetRequiredService<UserPreferencesService>();
    private readonly SessionEnrichmentService enrichment = services.GetRequiredService<SessionEnrichmentService>();

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        var conference = await database.GetConferenceAsync();
        var venue = conference.Venues.First(v => v.Id == city.VenueId);
        var allCitySessions = await database.GetSessionsAsync(city.Id);
        var favoriteTitles = allCitySessions
            .Where(s => preferences.IsFavorite(s.Id))
            .Select(s =>
            {
                var topic = enrichment.GetSession(s.Id).Topic;
                var tag = !string.IsNullOrWhiteSpace(topic) ? $" [{topic}]" : "";
                return $"{s.Time}{tag} {s.Title}";
            })
            .ToArray();

        return new AIContext
        {
            Instructions = $"""
                CURRENT USER & EVENT CONTEXT
                Event: {conference.Name}
                Selected City: {city.Name} | Event Date: {city.Date:dddd, yyyy-MM-dd}
                Current Conference Time: 08:30 (Morning before sessions start)
                Venue: {venue.Name} ({venue.Address})
                Saved Favourites: {(favoriteTitles.Length > 0 ? string.Join("; ", favoriteTitles) : "None yet")}
                """
        };
    }
}

/// <summary>
/// Context Provider 2: Injects the full day schedule of times, rooms, topics, and titles for the current city.
/// </summary>
public sealed class ScheduleContextProvider(IServiceProvider services) : AIContextProvider
{
    private readonly ConferenceDatabase database = services.GetRequiredService<ConferenceDatabase>();
    private readonly UserPreferencesService preferences = services.GetRequiredService<UserPreferencesService>();
    private readonly SessionEnrichmentService enrichment = services.GetRequiredService<SessionEnrichmentService>();

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        var conference = await database.GetConferenceAsync();
        var sessions = await database.GetSessionsAsync(city.Id);

        var lines = sessions.Select(session =>
        {
            var speakers = string.Join(", ", session.SpeakerIds.Select(id =>
                conference.Speakers.First(s => s.Id == id).Name));
            var topic = enrichment.GetSession(session.Id).Topic;
            var tag = !string.IsNullOrWhiteSpace(topic) ? $" [{topic}]" : "";
            var byline = !string.IsNullOrWhiteSpace(speakers) ? $" ({speakers})" : "";
            return $"- {session.Time} | {session.Track ?? "All attendees"}{tag} | {session.Title}{byline}";
        });

        return new AIContext
        {
            Instructions = $"""
                FULL DAY SCHEDULE FOR {city.Name.ToUpperInvariant()}
                {string.Join('\n', lines)}
                """
        };
    }
}

/// <summary>
/// Context Provider 3 (RAG): Finds the top matching sessions by semantic similarity and injects
/// each session's ID, title, and 1-line summary.
/// </summary>
public static class RagContextProvider
{
    private const int TopRagSessions = 3;

    public static TextSearchProvider Create(IServiceProvider services)
    {
        var database = services.GetRequiredService<ConferenceDatabase>();
        var preferences = services.GetRequiredService<UserPreferencesService>();
        var search = services.GetRequiredService<SearchService>();
        var enrichment = services.GetRequiredService<SessionEnrichmentService>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("ConferenceApp.AI.RagContextProvider");

        return new TextSearchProvider(
            async (query, cancellationToken) =>
            {
                var trimmed = string.Join(" ", query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (string.IsNullOrWhiteSpace(trimmed))
                    return [];

                var city = await preferences.GetSelectedCityAsync(database);
                var matches = (await search.SearchSessionsAsync(city.Id, trimmed))
                    .Where(session => session.IsTalk)
                    .Take(TopRagSessions)
                    .ToList();

                if (matches.Count == 0)
                    return [];

                logger.LogInformation("[AI RAG] Injected {Count} matching sessions in {City} for '{Query}': {Titles}",
                    matches.Count, city.Name, trimmed, string.Join(" | ", matches.Select(s => s.Title)));

                return matches.Select(session => new TextSearchProvider.TextSearchResult
                {
                    SourceName = session.Title,
                    SourceLink = session.Id,
                    Text = enrichment.GetSession(session.Id).Summary
                });
            },
            new TextSearchProviderOptions
            {
                SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
                SearchInputMessageFilter = messages => messages
                    .Where(m => m.Role == ChatRole.User && string.IsNullOrEmpty(m.AuthorName))
                    .TakeLast(1),
                CitationsPrompt = "Use the matching sessions above to identify the target session, then call get_session and get_speaker for full details."
            },
            loggerFactory);
    }
}
