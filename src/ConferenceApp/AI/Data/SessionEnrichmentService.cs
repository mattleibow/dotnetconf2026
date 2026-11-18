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
    : EnrichmentService<Session>(dbProvider, preferences, chatClient, logger)
{
    private const string TopicList = "AI, Agents, Cloud, Data, Mobile, Web, IoT, Architecture, Security, DevOps, Gaming";

    private static readonly string[] AllowedTopics = TopicList.Split(", ");

    protected override string Instructions =>
        $"""
        Using ONLY the supplied session title and abstract, produce SessionInsights:
        - Summary: a useful, concise summary in one or two sentences. Do not invent details.
        - Topics: one to three distinct topics chosen only from
          {TopicList}.
        If only a title is supplied, use only that title.
        """;

    protected override Task<List<Session>> GetItemsAsync() => GetTalkSessionsAsync();

    protected override async Task<bool> EnrichAsync(Session session)
    {
        var insights = await AskAsync<SessionInsights>(session.Title,
            $"Title: {session.Title}\n\nAbstract: {session.Description}");

        Apply(session, insights);

        if (!IsComplete(session))
        {
            var fallback = await AskAsync<SessionInsights>(session.Title, $"Title: {session.Title}");

            Apply(session, fallback);
        }

        return IsComplete(session);
    }

    private static bool IsComplete(Session session) =>
        !string.IsNullOrWhiteSpace(session.Summary) && session.Topics.Count > 0;

    private static void Apply(Session session, SessionInsights? insights)
    {
        if (insights is null)
            return;

        // Fill only empty fields so a retry preserves useful output from the first attempt.
        if (string.IsNullOrWhiteSpace(session.Summary) && !string.IsNullOrWhiteSpace(insights.Summary))
            session.Summary = insights.Summary.Trim();

        if (session.Topics.Count == 0)
        {
            var topics = (insights.Topics ?? [])
                .Select(topic => AllowedTopics.FirstOrDefault(allowed =>
                    string.Equals(allowed, topic?.Trim(), StringComparison.OrdinalIgnoreCase)))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();

            if (topics.Length > 0)
                session.Topics = topics;
        }
    }

    public sealed class SessionInsights
    {
        [Description("A useful, concise summary in one or two sentences, using only the supplied session details.")]
        public string Summary { get; set; } = "";

        [Description("One to three distinct topics chosen only from " + TopicList + ".")]
        public string[] Topics { get; set; } = [];
    }
}
