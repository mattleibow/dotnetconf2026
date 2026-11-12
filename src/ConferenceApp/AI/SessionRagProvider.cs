using ConferenceApp.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public static class SessionRagProvider
{
    private const int TopSessions = 3;

    public static TextSearchProvider Create(
        ConferenceDatabase database,
        SearchService search,
        ConferenceAssistantTools tools,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ConferenceApp.AI.SessionRagProvider");

        return new TextSearchProvider(
            async (query, cancellationToken) =>
            {
                var trimmed = string.Join(" ", query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (string.IsNullOrWhiteSpace(trimmed))
                    return [];

                var city = tools.CurrentCity;
                var matches = (await search.SearchSessionsAsync(city.Id, trimmed))
                    .Where(session => session.IsTalk)
                    .Take(TopSessions)
                    .ToList();

                var results = new List<TextSearchProvider.TextSearchResult>(matches.Count);
                foreach (var session in matches)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var speakers = await database.GetSpeakersForSessionAsync(session);
                    var speakerNames = string.Join(", ", speakers.Select(s => s.Name));
                    var abstractText = await database.GetTextAsync(session.Content);
                    results.Add(new TextSearchProvider.TextSearchResult
                    {
                        SourceName = $"{session.Title} ({session.Time}, {session.Track ?? "All attendees"}, Speakers: {speakerNames})",
                        SourceLink = session.Id,
                        Text = abstractText
                    });
                }

                logger.LogInformation("[AI RAG] TextSearchProvider injected {Count} session abstracts for '{Query}': {Titles}",
                    results.Count, trimmed, string.Join(" | ", matches.Select(s => s.Title)));
                return results;
            },
            new TextSearchProviderOptions
            {
                SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
                SearchInputMessageFilter = messages => messages.Where(m => m.Role == ChatRole.User).TakeLast(2),
                CitationsPrompt = "Use these session abstracts and tool results to answer accurately. Do not output raw SourceDocLink IDs."
            },
            loggerFactory);
    }
}
