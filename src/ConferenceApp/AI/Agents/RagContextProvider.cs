using ConferenceApp.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

/// <summary>
/// Finds the top matching sessions by semantic similarity and injects
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
