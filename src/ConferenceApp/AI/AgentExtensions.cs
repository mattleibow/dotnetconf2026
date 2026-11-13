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
        "Debate: Help me pick talks",
        "Debate which track or session I should attend",
        "Help me choose between sessions with a panel debate",
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
        var assistantClient = CreateAssistantAgent(services)
            .AsIChatClient();

        // Route 2 (Sequential Workflow): PreferencesAgent -> ScheduleAgent ("Plan my day").
        var dayPlanClient = CreateDayPlanWorkflow(services)
            .AsAIAgent(name: "DayPlanner")
            .AsIChatClient(allowNonChatClientAgents: true);

        // Route 3 (Group Chat Workflow): Concierge clarifies topics -> Depth vs Breadth vs Practical -> Moderator.
        var debateClient = CreateDebateWorkflow(services)
            .AsAIAgent(name: "PanelDebate")
            .AsIChatClient(allowNonChatClientAgents: true);

        // Semantic router: uses our ONNX MiniLM embeddings to pick the right agent or workflow per prompt.
        var semanticRouter = new SemanticRoutingChatClient(
            services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
            new Dictionary<IChatClient, IReadOnlyList<string>>
            {
                [dayPlanClient] = DayPlanUtterances,
                [debateClient] = DebateUtterances
            },
            defaultClient: assistantClient,
            scoreThreshold: 0.60f,
            topK: 1,
            leaveOpen: true);

        // RoutingChatClient inspects the full conversation history in RoutingContext.Messages:
        // if the previous assistant message was Concierge asking the user to confirm their topics,
        // keep the follow-up reply on the Debate workflow; otherwise delegate to SemanticRoutingChatClient.
        return RoutingChatClient.Create((context, _) =>
        {
            var lastAssistant = context.Messages.LastOrDefault(m =>
                m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.AuthorName));
            if (lastAssistant?.AuthorName == "Concierge")
                return ValueTask.FromResult(debateClient);

            return ValueTask.FromResult<IChatClient>(semanticRouter);
        });
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

    // 3. Group Chat Workflow: Concierge first clarifies/confirms the user's interests (using their
    //    saved favourites as a starting point), then DepthAdvocate, BreadthAdvocate, PracticalAdvocate,
    //    and Moderator debate which sessions they should attend.
    private static Workflow CreateDebateWorkflow(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = new UserContextProvider(services);
        var agendaContext = new AgendaContextProvider(services);

        var concierge = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Concierge",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are Concierge, hosting the conference session debate panel.

                        If you have NOT yet asked the attendee to confirm their topics:
                        - If Saved Favourites is "None yet" and the attendee did not
                          name specific topics, ask what topics they are interested in
                          today (such as AI, Cloud, Data, Mobile, Web, or IoT).
                        - If they have Saved Favourites or named topics, mention those
                          topics as a starting point and ask if they are happy to debate
                          around them or want to expand into other topics (such as AI,
                          Cloud, Data, Mobile, Web, or IoT).
                        Keep this to 1 or 2 short sentences and do NOT pitch talks yet.

                        If the attendee is replying to confirm or choose their topics:
                        - Output ONLY one short line:
                          "Confirmed topics: <topics>."
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 140
                },
                AIContextProviders = [userContext]
            },
            loggerFactory,
            services);

        var depthAdvocate = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "DepthAdvocate",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are DepthAdvocate on the conference panel.

                        Pitch 1 or 2 sessions from the current city's agenda that dive
                        deepest into the attendee's Confirmed topics in 2 short
                        sentences. Always use exact session titles and times.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContext, agendaContext]
            },
            loggerFactory,
            services);

        var breadthAdvocate = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "BreadthAdvocate",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are BreadthAdvocate on the conference panel.

                        Look at what DepthAdvocate pitched and the attendee's Confirmed
                        topics, then pitch 1 or 2 sessions from a DIFFERENT topic in the
                        current city's agenda to expand their horizons in 2 short
                        sentences. Always use exact session titles and times.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContext, agendaContext]
            },
            loggerFactory,
            services);

        var practicalAdvocate = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "PracticalAdvocate",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are PracticalAdvocate on the conference panel.

                        Pitch 1 or 2 hands-on, code-first or demo-heavy sessions from
                        the current city's agenda that the attendee can apply right away
                        in 2 short sentences. Always use exact session titles and times.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContext, agendaContext]
            },
            loggerFactory,
            services);

        var moderator = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Moderator",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are Moderator wrapping up the panel debate.

                        In 2 short sentences, weigh DepthAdvocate, BreadthAdvocate, and
                        PracticalAdvocate's picks and give the attendee a clear takeaway
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
            .CreateGroupChatBuilderWith(agents => new RoundRobinGroupChatManager(
                agents,
                (manager, history, _) =>
                {
                    // Before the panel debates, pause after Concierge (Iteration 1) so the user
                    // can confirm or adjust their topics. Once the user replies to Concierge,
                    // run all 5 participants (Concierge -> Depth -> Breadth -> Practical -> Moderator).
                    var messages = history.ToList();
                    var lastUserIndex = messages.FindLastIndex(m =>
                        m.Role == ChatRole.User && string.IsNullOrEmpty(m.AuthorName));
                    var userConfirmedAfterConcierge = lastUserIndex > 0 &&
                        messages.Take(lastUserIndex).Any(m => m.AuthorName == "Concierge");

                    if (!userConfirmedAfterConcierge)
                        return ValueTask.FromResult(manager.IterationCount >= 1);

                    return ValueTask.FromResult(manager.IterationCount >= agents.Count);
                }))
            .AddParticipants(concierge, depthAdvocate, breadthAdvocate, practicalAdvocate, moderator)
            .WithName("PanelDebateWorkflow")
            .Build();
    }

    // Context Provider 1: Injects the user's current city, event date, venue, and saved favourites.
    private sealed class UserContextProvider(IServiceProvider services) : AIContextProvider
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
