using ConferenceApp.Data;
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
    private readonly ConferenceDbProvider dbProvider = services.GetRequiredService<ConferenceDbProvider>();
    private readonly UserPreferencesService preferences = services.GetRequiredService<UserPreferencesService>();

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var favoriteTitles = db.Sessions
            .Where(s => s.CityId == city.Id && preferences.IsFavorite(s.Id))
            .Select(s =>
            {
                var tag = !string.IsNullOrWhiteSpace(s.Topic) ? $" [{s.Topic}]" : "";
                return $"{s.Time}{tag} {s.Title}";
            })
            .ToArray();

        return new AIContext
        {
            Instructions = $"""
                CURRENT USER & EVENT CONTEXT
                Event: {db.Name}
                Selected City: {city.Name} | Event Date: {city.Date:dddd, yyyy-MM-dd}
                Current Conference Time: 08:30 (Morning before sessions start)
                Venue: {city.Venue.Name} ({city.Venue.Address})
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
    private readonly ConferenceDbProvider dbProvider = services.GetRequiredService<ConferenceDbProvider>();
    private readonly UserPreferencesService preferences = services.GetRequiredService<UserPreferencesService>();

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var sessions = db.Sessions.Where(s => s.CityId == city.Id);

        var lines = sessions.Select(session =>
        {
            var tag = !string.IsNullOrWhiteSpace(session.Topic) ? $" [{session.Topic}]" : "";
            var byline = !string.IsNullOrWhiteSpace(session.SpeakerNames) ? $" ({session.SpeakerNames})" : "";
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
        var preferences = services.GetRequiredService<UserPreferencesService>();
        var search = services.GetRequiredService<SearchService>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("ConferenceApp.AI.RagContextProvider");

        return new TextSearchProvider(
            async (query, cancellationToken) =>
            {
                var trimmed = string.Join(" ", query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (string.IsNullOrWhiteSpace(trimmed))
                    return [];

                var city = await preferences.GetSelectedCityAsync();
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
                    Text = session.Summary ?? ""
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
