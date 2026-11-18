using System.ComponentModel;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public sealed class SessionEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IChatClient chatClient,
    ILogger<SessionEnrichmentService> logger)
    : EnrichmentService(dbProvider, preferences, chatClient, logger)
{
    private static readonly string[] AllowedTopics =
    [
        "AI", "Agents", "Cloud", "Data", "Mobile", "Web",
        "IoT", "Architecture", "Security", "DevOps", "Gaming"
    ];

    protected override string Instructions =>
        """
        Using ONLY the supplied session title and abstract, produce SessionInsights:
        - Summary: a useful, concise summary in one or two sentences. Do not invent details.
        - Topics: one to three distinct topics chosen only from
          AI, Agents, Cloud, Data, Mobile, Web, IoT, Architecture, Security, DevOps, Gaming.
        If only a title is supplied, use only that title.
        """;

    protected override async Task<int> EnrichAsync()
    {
        var enriched = 0;
        foreach (var session in await GetTalkSessionsAsync())
        {
            var (insights, timedOut) = await GetResponseAsync<SessionInsights>(
                $"Title: {session.Title}\n\nAbstract: {session.Description}");
            if (timedOut)
                continue;

            Apply(session, insights);
            if (string.IsNullOrWhiteSpace(session.Summary) || session.Topics.Count == 0)
            {
                var (fallback, fallbackTimedOut) = await GetResponseAsync<SessionInsights>($"Title: {session.Title}");
                if (fallbackTimedOut)
                    continue;

                Apply(session, fallback);
            }

            if (!string.IsNullOrWhiteSpace(session.Summary) && session.Topics.Count > 0)
                enriched++;
            else
                Logger.LogWarning("[AI] Session {SessionId} has incomplete insights after enrichment.", session.Id);
        }

        return enriched;
    }

    private static void Apply(Session session, SessionInsights? insights)
    {
        if (insights is null)
            return;

        if (string.IsNullOrWhiteSpace(session.Summary) && !string.IsNullOrWhiteSpace(insights.Summary))
            session.Summary = insights.Summary.Trim();

        if (session.Topics.Count == 0)
            session.Topics = (insights.Topics ?? [])
                .Select(topic => AllowedTopics.FirstOrDefault(allowed =>
                    string.Equals(allowed, topic?.Trim(), StringComparison.OrdinalIgnoreCase)))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();
    }

    public sealed class SessionInsights
    {
        [Description("A useful, concise summary in one or two sentences, using only the supplied session details.")]
        public string Summary { get; set; } = "";

        [Description("One to three distinct topics chosen only from AI, Agents, Cloud, Data, Mobile, Web, IoT, Architecture, Security, DevOps, Gaming.")]
        public string[] Topics { get; set; } = [];
    }
}
