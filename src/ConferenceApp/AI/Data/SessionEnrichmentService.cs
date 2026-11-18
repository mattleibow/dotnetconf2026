using System.ComponentModel;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public sealed class SessionEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IChatClient chatClient,
    ILogger<SessionEnrichmentService> logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    private static readonly string[] AllowedTopics =
    [
        "AI", "Agents", "Cloud", "Data", "Mobile", "Web",
        "IoT", "Architecture", "Security", "DevOps", "Gaming"
    ];

    private const string SessionPrompt =
        """
        Using ONLY the supplied session title and abstract, produce SessionInsights:
        - Summary: a useful, concise summary in one or two sentences. Do not invent details.
        - Topics: one to three distinct topics chosen only from
          AI, Agents, Cloud, Data, Mobile, Web, IoT, Architecture, Security, DevOps, Gaming.
        If only a title is supplied, use only that title.
        """;

    protected override async Task ExecuteAsync()
    {
        if (await ChatExtensions.GetModelAvailabilityAsync(logger: Logger) != ChatModelAvailability.Available)
            return;

        var enriched = 0;
        foreach (var session in await GetTalkSessionsAsync())
        {
            var (insights, timedOut) = await EnrichmentRequest.GetAsync<SessionInsights>(
                chatClient, Logger, SessionPrompt,
                $"Title: {session.Title}\n\nAbstract: {session.Description}", "session", session.Id);
            if (timedOut)
                continue;

            await MainThread.InvokeOnMainThreadAsync(() => ApplySessionInsights(session, insights));
            if (string.IsNullOrWhiteSpace(session.Summary) || session.Topics.Count == 0)
            {
                var (fallback, fallbackTimedOut) = await EnrichmentRequest.GetAsync<SessionInsights>(
                    chatClient, Logger, SessionPrompt, $"Title: {session.Title}", "session-title", session.Id);
                if (fallbackTimedOut)
                    continue;

                await MainThread.InvokeOnMainThreadAsync(() => ApplySessionInsights(session, fallback));
            }

            if (!string.IsNullOrWhiteSpace(session.Summary) && session.Topics.Count > 0)
                enriched++;
            else
                Logger.LogWarning("[AI] Session {SessionId} has incomplete insights after enrichment.", session.Id);
        }

        Logger.LogInformation("[AI] Session enrichment completed. Sessions: {SessionCount}.", enriched);
    }

    private static void ApplySessionInsights(Session session, SessionInsights? insights)
    {
        if (insights is null)
            return;

        // A title-only fallback fills gaps without replacing independently valid fields.
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
