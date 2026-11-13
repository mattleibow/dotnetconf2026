using ConferenceApp.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public static class AgentExtensions
{
    private const int TopRagSessions = 3;

    private static readonly IReadOnlyList<string> DeepDiveUtterances =
    [
        "Give me a deep dive on this session",
        "Prepare me for this talk: takeaways and questions to ask",
        "What are the key takeaways and questions to ask the speaker?",
        "Give me a deep dive, what to expect, and questions to ask in Q&A"
    ];

    private static readonly IReadOnlyList<string> RecommendationUtterances =
    [
        "Can you suggest some talks for me?",
        "What sessions or talks do you recommend I attend?",
        "Help me pick or choose which talks to go to",
        "Plan my day",
        "Plan my day: I'm into mobile and AI, leaving at 3",
        "Debate: Help me pick talks"
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
        // Route 1 (Default): Generic assistant with the full day schedule in context + all tools.
        var assistantClient = CreateAssistantAgent(services)
            .AsIChatClient();

        // Route 2 (Sequential + Concurrent Fan-Out): Researcher -> parallel [TakeawaysAgent, QuestionsCoachAgent].
        var deepDiveClient = CreateDeepDiveWorkflow(services)
            .AsAIAgent(name: "SessionDeepDive")
            .AsIChatClient(allowNonChatClientAgents: true);

        // Route 3 (Multi-Turn Group Chat): Concierge clarifies topics/times -> Depth/Breadth/Practical -> Timekeeper -> Moderator.
        var recommendationClient = CreateRecommendationWorkflow(services)
            .AsAIAgent(name: "TalkRecommendations")
            .AsIChatClient(allowNonChatClientAgents: true);

        // Semantic router: uses our ONNX MiniLM embeddings to pick the right agent or workflow per prompt.
        var semanticRouter = new SemanticRoutingChatClient(
            services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
            new Dictionary<IChatClient, IReadOnlyList<string>>
            {
                [deepDiveClient] = DeepDiveUtterances,
                [recommendationClient] = RecommendationUtterances
            },
            defaultClient: assistantClient,
            scoreThreshold: 0.58f,
            topK: 1,
            leaveOpen: true);

        // RoutingChatClient inspects the full conversation history in RoutingContext.Messages:
        // if the previous assistant message was Concierge asking the user to confirm their topics,
        // keep the follow-up reply on the Recommendation workflow; otherwise delegate to SemanticRoutingChatClient.
        return RoutingChatClient.Create((context, _) =>
        {
            var lastAssistant = context.Messages.LastOrDefault(m =>
                m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.AuthorName));
            if (lastAssistant?.AuthorName == "Concierge")
                return ValueTask.FromResult(recommendationClient);

            return ValueTask.FromResult<IChatClient>(semanticRouter);
        });
    }

    // 1. Generic Assistant: Fast everyday actions (times, rooms, speaker bios, and managing favourites).
    //    Includes UserContext + ScheduleContext (all times & titles) + RAG summaries + Data & Favourites tools.
    private static AIAgent CreateAssistantAgent(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = new UserContextProvider(services);
        var scheduleContext = new ScheduleContextProvider(services);
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
                        Answer briefly using the provided city context, full day
                        schedule, RAG session summaries, and tool results.

                        The city's full schedule of times, rooms, and titles is already
                        in your context, so you can answer schedule and time-clash
                        questions immediately without calling search_sessions.

                        Call get_session when you need a talk's full abstract,
                        get_speaker for a speaker's biography, or get_favorites,
                        add_favorite, and remove_favorite to manage saved talks.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 700,
                    Tools =
                    [
                        .. ConferenceDataToolContext.Default.Tools,
                        .. FavoritesToolContext.Default.Tools
                    ]
                },
                AIContextProviders = [userContext, scheduleContext, ragContext]
            },
            loggerFactory,
            services);
    }

    // 2. Session Deep-Dive Workflow (Sequential -> Concurrent Fan-Out):
    //    Step 1 (SessionResearcher) gathers the session abstract and speaker bio via tools;
    //    Step 2 fans out concurrently to TakeawaysAgent (what to expect) and QuestionsCoachAgent (questions to ask).
    private static Workflow CreateDeepDiveWorkflow(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = new UserContextProvider(services);
        var scheduleContext = new ScheduleContextProvider(services);

        var sessionResearcher = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "SessionResearcher",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        Find the specific session the attendee is asking about from the
                        city schedule in context. Call get_session to read its full
                        abstract and call get_speaker to read the speaker's biography.

                        Summarize the session title, time, room, speaker bio, and key
                        abstract points in a concise factual brief for the analysts.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 350,
                    Tools = [.. ConferenceDataToolContext.Default.Tools]
                },
                AIContextProviders = [userContext, scheduleContext]
            },
            loggerFactory,
            services);

        var takeawaysAgent = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "TakeawaysAgent",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        Using SessionResearcher's brief, write 3 concise bullet points
                        explaining what to expect in this session and the key technical
                        takeaways for a developer. Include the exact session title and
                        time in the first bullet.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 240
                },
                AIContextProviders = [userContext]
            },
            loggerFactory,
            services);

        var questionsCoachAgent = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "QuestionsCoachAgent",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        Using SessionResearcher's brief, suggest 2 thoughtful things to
                        think about during the talk and 2 smart questions the attendee
                        can ask the speaker during Q&A.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 240
                },
                AIContextProviders = [userContext]
            },
            loggerFactory,
            services);

        var parallelAnalysis = AgentWorkflowBuilder
            .BuildConcurrent("DeepDiveParallelAnalysis", [takeawaysAgent, questionsCoachAgent])
            .AsAIAgent(name: "DeepDiveParallelAnalysis");

        return AgentWorkflowBuilder.BuildSequential(
            "SessionDeepDiveWorkflow",
            [sessionResearcher, parallelAnalysis]);
    }

    // 3. Talk Recommendation & Debate Workflow (Multi-Turn Group Chat):
    //    Turn 1: Concierge clarifies/confirms topics (using saved favourites as a starting point) & time window.
    //    Turn 2: DepthAdvocate -> BreadthAdvocate -> PracticalAdvocate -> Timekeeper -> Moderator.
    private static Workflow CreateRecommendationWorkflow(IServiceProvider services)
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
                        You are Concierge, hosting the conference talk recommendation
                        and debate panel.

                        If you have NOT yet asked the attendee to confirm their topics:
                        - If Saved Favourites is "None yet" and the attendee did not
                          name specific topics, ask what topics they are interested in
                          today (such as AI, Cloud, Data, Mobile, Web, or IoT) and
                          whether they need to arrive late or leave early.
                        - If they have Saved Favourites or named topics, mention those
                          topics (and any arrival/departure time they gave) as a
                          starting point, and ask if they are happy with that or want
                          to explore other topics too.
                        Keep this to 1 or 2 short sentences and do NOT pitch talks yet.

                        If the attendee is replying to confirm or choose their topics:
                        - Output ONLY these two short lines (default Window 08:00–17:00;
                          "leaving at 3" means 08:00–15:00):
                          Confirmed topics: <topics>
                          Time window: <HH:mm>–<HH:mm>
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 150
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

        var timekeeper = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Timekeeper",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are Timekeeper on the conference panel.

                        Check the sessions pitched by DepthAdvocate, BreadthAdvocate,
                        and PracticalAdvocate against the attendee's Time window and the
                        current time in context. In 2 short sentences, flag any talks
                        that clash in the same time slot or fall outside the attendee's
                        Time window, and state which ones fit cleanly.
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

                        Using the advocates' pitches and Timekeeper's schedule check,
                        present a final conflict-free recommended list of 2 to 4 talks
                        (with exact times and titles) in 2 or 3 short sentences.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 200
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
                    // run all 6 participants (Concierge -> Depth -> Breadth -> Practical -> Timekeeper -> Moderator).
                    var messages = history.ToList();
                    var lastUserIndex = messages.FindLastIndex(m =>
                        m.Role == ChatRole.User && string.IsNullOrEmpty(m.AuthorName));
                    var userConfirmedAfterConcierge = lastUserIndex > 0 &&
                        messages.Take(lastUserIndex).Any(m => m.AuthorName == "Concierge");

                    if (!userConfirmedAfterConcierge)
                        return ValueTask.FromResult(manager.IterationCount >= 1);

                    return ValueTask.FromResult(manager.IterationCount >= agents.Count);
                }))
            .AddParticipants(concierge, depthAdvocate, breadthAdvocate, practicalAdvocate, timekeeper, moderator)
            .WithName("TalkRecommendationWorkflow")
            .Build();
    }

    // Context Provider 1: Injects the user's current city, event date, current time, venue, and saved favourites.
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
                    Selected City: {city.Name} | Event Date: {city.Date:dddd, yyyy-MM-dd}
                    Current Conference Time: 08:30 (Morning before sessions start)
                    Venue: {venue.Name} ({venue.Address})
                    Saved Favourites: {(favoriteTitles.Length > 0 ? string.Join("; ", favoriteTitles) : "None yet")}
                    """
            };
        }
    }

    // Context Provider 2: Injects the full day schedule of times, rooms, and titles for the generic assistant.
    private sealed class ScheduleContextProvider(IServiceProvider services) : AIContextProvider
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

    // Context Provider 3: Injects the talk catalog with 1-line summaries for the Group Chat recommendation panel.
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

    // Context Provider 4 (RAG): Finds the top matching sessions by semantic similarity and injects
    // each session's ID, title, and summary so SessionResearcher can call get_session / get_speaker.
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
                CitationsPrompt = "Use the matching sessions above to identify the target session, then call get_session and get_speaker for full details."
            },
            loggerFactory);
    }
}
