using ConferenceApp.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public static class AgentExtensions
{
    private const int TopRagSessions = 5;

    private static readonly IReadOnlyList<string> DayPlanUtterances =
    [
        "Plan my day",
        "Plan my day: I'm into mobile and AI, leaving at 3",
        "Build me a conflict-free schedule for today",
        "Create a full day itinerary of sessions with no clashes"
    ];

    private static readonly IReadOnlyList<string> DebateUtterances =
    [
        "Debate: AI vs Cloud talks",
        "Debate which track or session I should attend",
        "Help me choose between the AI talks and the cloud architecture talks",
        "Give me different perspectives on which sessions to pick"
    ];

    public static MauiAppBuilder AddAgents(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<ConferenceDataTools>();
        builder.Services.AddSingleton<FavoritesTools>();
        builder.Services.AddKeyedChatClient("assistant", CreateAssistantRouter);

        return builder;
    }

    private static IChatClient CreateAssistantRouter(IServiceProvider services)
    {
        // Route 1 (Default): Single AIAgent with user context, RAG search, and all tools.
        var assistantAgent = CreateAssistantAgent(services);

        // Route 2 (Sequential Workflow): PreferencesAgent -> ScheduleAgent ("Plan my day").
        var dayPlanWorkflow = CreateDayPlanWorkflow(services);

        // Route 3 (Group Chat Workflow): Round-robin multi-agent panel debate ("Debate: AI vs Cloud talks").
        var debateWorkflow = CreateDebateWorkflow(services);

        // Semantic router: uses our ONNX MiniLM embeddings to pick the right agent or workflow per prompt.
        return new SemanticRoutingChatClient(
            services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
            new Dictionary<IChatClient, IReadOnlyList<string>>
            {
                [dayPlanWorkflow.AsAIAgent(name: "DayPlanner").AsIChatClient(allowNonChatClientAgents: true)] = DayPlanUtterances,
                [debateWorkflow.AsAIAgent(name: "PanelDebate").AsIChatClient(allowNonChatClientAgents: true)] = DebateUtterances
            },
            defaultClient: assistantAgent.AsIChatClient(),
            scoreThreshold: 0.60f,
            topK: 1,
            leaveOpen: true);
    }

    // 1. Single Agent: General Q&A, speaker bios, schedule queries, and favourites.
    //    Uses UserContext + RAG (to discover matching sessions) + both Data and Favourites tool sets.
    private static AIAgent CreateAssistantAgent(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = new UserContextProvider(services);
        var ragContext = CreateRagProvider(services);

        return localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "ConferenceAssistant",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are the .NET Conf South Africa assistant.
                        Answer briefly using only the provided city context, matching
                        session summaries, and tool results.

                        The RAG context lists matching sessions by ID, title, and summary.
                        Call get_session when you need a session's time, room, speakers,
                        or full abstract. Call get_speaker for a speaker's biography,
                        or list_sessions for a time window.

                        Call get_favorites, add_favorite, or remove_favorite to manage
                        the user's saved talks.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 700,
                    Tools =
                    [
                        .. ConferenceDataToolContext.Default.Tools,
                        .. FavoritesToolContext.Default.Tools
                    ]
                },
                AIContextProviders = [userContext, ragContext]
            },
            loggerFactory,
            services);
    }

    // 2. Sequential Workflow: Step 1 extracts arrival/departure & topics; Step 2 builds a conflict-free schedule.
    //    Only gives ScheduleAgent the read-only ConferenceDataTools (no favourite mutation tools).
    private static Workflow CreateDayPlanWorkflow(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = new UserContextProvider(services);

        var preferencesAgent = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "PreferencesAgent",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        Extract the attendee's schedule preferences from their request.
                        Do NOT list or pick any talks or sessions.

                        Output ONLY these three lines in 24-hour daytime format
                        ("leaving at 3" means 15:00; default Arrive 08:00, Leave 17:00):

                        Topics: <topics or All>
                        Arrive: <HH:mm>
                        Leave: <HH:mm>
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 120
                },
                AIContextProviders = [userContext]
            },
            loggerFactory,
            services);

        var scheduleAgent = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "ScheduleAgent",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        Build a conflict-free schedule of talks for the current city
                        that fits the attendee's Topics, Arrive, and Leave lines.

                        Call list_sessions from Arrive to Leave to see the available
                        time slots, then pick one talk per slot. Never pick two talks
                        with overlapping times.

                        List every chosen talk as a bullet with its time, exact title,
                        room, and one short sentence on why it fits.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 700,
                    Tools = [.. ConferenceDataToolContext.Default.Tools]
                },
                AIContextProviders = [userContext]
            },
            loggerFactory,
            services);

        return AgentWorkflowBuilder.BuildSequential(
            "DayPlanWorkflow",
            [preferencesAgent, scheduleAgent]);
    }

    // 3. Group Chat Workflow: Three specialist personas take turns debating which talks to attend.
    //    Uses AgendaContextProvider so all panelists see the city's full talk catalog without tool calls.
    private static Workflow CreateDebateWorkflow(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = new UserContextProvider(services);
        var agendaContext = new AgendaContextProvider(services);

        var practicalBuilder = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "PracticalBuilder",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are PracticalBuilder, a hands-on mobile and AI app developer
                        on a conference panel.

                        Pitch 1 or 2 practical, code-heavy or AI/mobile talks from the
                        current city's agenda in 2 short sentences. Always use exact
                        session titles and times from the context.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContext, agendaContext]
            },
            loggerFactory,
            services);

        var systemArchitect = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "SystemArchitect",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are SystemArchitect, a cloud, architecture, and DevOps lead
                        on a conference panel.

                        Counter PracticalBuilder by pitching 1 or 2 architecture, cloud,
                        or platform talks from the current city's agenda in 2 short
                        sentences. Always use exact session titles and times.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContext, agendaContext]
            },
            loggerFactory,
            services);

        var trackModerator = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "TrackModerator",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are TrackModerator wrapping up the panel debate.

                        In 2 short sentences, synthesize PracticalBuilder and
                        SystemArchitect's picks and give the attendee a clear takeaway
                        on which talk to attend first.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContext]
            },
            loggerFactory,
            services);

        return AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(agents => new RoundRobinGroupChatManager(agents)
            {
                MaximumIterationCount = 3
            })
            .AddParticipants(practicalBuilder, systemArchitect, trackModerator)
            .WithName("PanelDebateWorkflow")
            .Build();
    }

    // Context Provider 1: Injects the user's current city, event date, venue, and saved favourites.
    private sealed class UserContextProvider(IServiceProvider services) : AIContextProvider
    {
        private readonly ConferenceDatabase database = services.GetRequiredService<ConferenceDatabase>();
        private readonly UserPreferencesService preferences = services.GetRequiredService<UserPreferencesService>();

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
                .Select(s => $"{s.Time} {s.Title}")
                .ToArray();

            return new AIContext
            {
                Instructions = $"""
                    CURRENT USER & EVENT CONTEXT
                    Event: {conference.Name}
                    Selected City: {city.Name} | Date: {city.Date:dddd, yyyy-MM-dd}
                    Venue: {venue.Name} ({venue.Address})
                    Saved Favourites: {(favoriteTitles.Length > 0 ? string.Join("; ", favoriteTitles) : "None yet")}
                    """
            };
        }
    }

    // Context Provider 2: Injects the compact talk catalog for the current city (used by the Group Chat debate).
    private sealed class AgendaContextProvider(IServiceProvider services) : AIContextProvider
    {
        private readonly ConferenceDatabase database = services.GetRequiredService<ConferenceDatabase>();
        private readonly UserPreferencesService preferences = services.GetRequiredService<UserPreferencesService>();
        private readonly SessionEnrichmentService enrichment = services.GetRequiredService<SessionEnrichmentService>();

        protected override async ValueTask<AIContext> ProvideAIContextAsync(
            InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            var city = await preferences.GetSelectedCityAsync(database);
            var talks = (await database.GetSessionsAsync(city.Id)).Where(s => s.IsTalk);

            var lines = talks.Select(session =>
            {
                var enriched = enrichment.GetSession(session.Id);
                var topic = enriched.HasTopic ? $" [{enriched.Topic}]" : "";
                var summary = !string.IsNullOrWhiteSpace(enriched.Summary) ? $" — {enriched.Summary}" : "";
                return $"- {session.Time} | {session.Track ?? "All attendees"}{topic} | {session.Title}{summary}";
            });

            return new AIContext
            {
                Instructions = $"""
                    AVAILABLE TALKS IN {city.Name.ToUpperInvariant()}
                    {string.Join('\n', lines)}
                    """
            };
        }
    }

    // Context Provider 3 (RAG): Finds the top matching sessions by semantic similarity and injects
    // each session's ID, title, and summary for discovery. The agent calls get_session for schedule/abstract details.
    private static TextSearchProvider CreateRagProvider(IServiceProvider services)
    {
        var database = services.GetRequiredService<ConferenceDatabase>();
        var preferences = services.GetRequiredService<UserPreferencesService>();
        var search = services.GetRequiredService<SearchService>();
        var enrichment = services.GetRequiredService<SessionEnrichmentService>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("ConferenceApp.AI.AgentExtensions");

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
                CitationsPrompt = "Use the matching sessions above to identify relevant talks, then call get_session when you need schedule times, rooms, speakers, or the full abstract."
            },
            loggerFactory);
    }
}
