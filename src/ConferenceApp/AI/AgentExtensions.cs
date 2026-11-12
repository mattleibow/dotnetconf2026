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
        builder.Services.AddScoped<ConferenceAssistantTools>();
        builder.Services.AddScoped<SemanticRoutingChatClient>(CreateAssistantRouter);

        return builder;
    }

    private static SemanticRoutingChatClient CreateAssistantRouter(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();

        var userContextProvider = new UserContextProvider(
            services.GetRequiredService<ConferenceDatabase>(),
            services.GetRequiredService<UserPreferencesService>());
        var ragProvider = CreateRagProvider(services, loggerFactory);

        // Route 1 (Default): Single AIAgent with user context, RAG summaries, and source-generated tools.
        var assistantAgent = CreateAssistantAgent(localClient, userContextProvider, ragProvider, loggerFactory, services);

        // Route 2 (Sequential Workflow): PreferencesAgent -> ScheduleAgent ("Plan my day").
        var dayPlanWorkflow = CreateDayPlanWorkflow(localClient, userContextProvider, ragProvider, loggerFactory, services);

        // Route 3 (Group Chat Workflow): Round-robin multi-agent panel debate ("Debate: AI vs Cloud talks").
        var debateWorkflow = CreateDebateWorkflow(localClient, userContextProvider, ragProvider, loggerFactory, services);

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

    // 1. Single Agent: Fast Q&A, speaker bios, schedule queries, and human-in-the-loop favourites.
    private static AIAgent CreateAssistantAgent(
        IChatClient localClient,
        AIContextProvider userContextProvider,
        AIContextProvider ragProvider,
        ILoggerFactory loggerFactory,
        IServiceProvider services) =>
        localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "ConferenceAssistant",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are the .NET Conf South Africa assistant.
                        Answer briefly using only the provided city context, matching session summaries, and tool results.
                        The context includes 1-line summaries of matching talks; call get_session when you need a full abstract,
                        get_speaker for a speaker's biography, list_sessions for a time window, or get_favorites for saved talks.
                        Favourite tools display an inline approval card; never claim a change succeeded before the tool returns.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 700,
                    Tools = [.. ConferenceToolContext.Default.Tools]
                },
                AIContextProviders = [userContextProvider, ragProvider]
            },
            loggerFactory,
            services);

    // 2. Sequential Workflow: Step 1 extracts arrival/departure & topics; Step 2 builds a conflict-free schedule.
    private static Workflow CreateDayPlanWorkflow(
        IChatClient localClient,
        AIContextProvider userContextProvider,
        AIContextProvider ragProvider,
        ILoggerFactory loggerFactory,
        IServiceProvider services)
    {
        var preferencesAgent = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "PreferencesAgent",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        Do NOT list or pick any talks or sessions.
                        Output ONLY one short line in this exact format:
                        "Preferences — Topics: <topics>; Arrive: <HH:mm>; Leave: <HH:mm>. Now build the full schedule."
                        Use 24-hour daytime ("leaving at 3" means 15:00; default Arrive 08:00, Leave 17:00).
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 120
                },
                AIContextProviders = [userContextProvider]
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
                        Build a conflict-free schedule of talks for the current city that fits the attendee's
                        Preferences line and falls between their Arrive and Leave times.
                        Call list_sessions from Arrive to Leave to see the available time slots, then pick one talk per slot.
                        Never pick two talks with overlapping times.
                        List every chosen talk as a bullet with its time, exact title, room, and one short sentence on why it fits.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 700,
                    Tools = [.. ConferenceToolContext.Default.Tools]
                },
                AIContextProviders = [userContextProvider, ragProvider]
            },
            loggerFactory,
            services);

        return AgentWorkflowBuilder.BuildSequential(
            "DayPlanWorkflow",
            [preferencesAgent, scheduleAgent]);
    }

    // 3. Group Chat Workflow: Three specialist personas take turns debating which talks to attend.
    private static Workflow CreateDebateWorkflow(
        IChatClient localClient,
        AIContextProvider userContextProvider,
        AIContextProvider ragProvider,
        ILoggerFactory loggerFactory,
        IServiceProvider services)
    {
        var practicalBuilder = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "PracticalBuilder",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are PracticalBuilder, a hands-on mobile & AI app developer on a conference panel.
                        Pitch 1 or 2 practical, code-heavy or AI/mobile talks from the current city's sessions in 2 short sentences.
                        Always use exact session titles and times from the context.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContextProvider, ragProvider]
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
                        You are SystemArchitect, a cloud, architecture, and DevOps lead on a conference panel.
                        Counter PracticalBuilder by pitching 1 or 2 architecture, cloud, or platform talks from the current city's sessions in 2 short sentences.
                        Always use exact session titles and times from the context.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContextProvider, ragProvider]
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
                        In 2 short sentences, synthesize PracticalBuilder and SystemArchitect's picks and give the attendee a clear takeaway on which talk to pick first.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContextProvider]
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
    private sealed class UserContextProvider(
        ConferenceDatabase database,
        UserPreferencesService preferences) : AIContextProvider
    {
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

    // Context Provider 2 (RAG): Injects the top matching session summaries (title + topic + 1-line summary)
    // before each turn. For full multi-paragraph abstracts, the model calls the get_session tool on demand.
    private static TextSearchProvider CreateRagProvider(IServiceProvider services, ILoggerFactory loggerFactory)
    {
        var database = services.GetRequiredService<ConferenceDatabase>();
        var preferences = services.GetRequiredService<UserPreferencesService>();
        var search = services.GetRequiredService<SearchService>();
        var tools = services.GetRequiredService<ConferenceAssistantTools>();
        var logger = loggerFactory.CreateLogger("ConferenceApp.AI.AgentExtensions");

        return new TextSearchProvider(
            async (query, cancellationToken) =>
            {
                var trimmed = string.Join(" ", query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (string.IsNullOrWhiteSpace(trimmed))
                    return [];

                var city = await preferences.GetSelectedCityAsync(database);
                var conference = await database.GetConferenceAsync();
                var matches = (await search.SearchSessionsAsync(city.Id, trimmed))
                    .Where(session => session.IsTalk)
                    .Take(TopRagSessions)
                    .ToList();

                if (matches.Count == 0)
                    return [];

                logger.LogInformation("[AI RAG] Injected {Count} session summaries in {City} for '{Query}': {Titles}",
                    matches.Count, city.Name, trimmed, string.Join(" | ", matches.Select(s => s.Title)));

                return matches.Select(session => new TextSearchProvider.TextSearchResult
                {
                    SourceName = session.Title,
                    SourceLink = session.Id,
                    Text = tools.FormatSession(conference, session)
                });
            },
            new TextSearchProviderOptions
            {
                SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
                SearchInputMessageFilter = messages => messages
                    .Where(m => m.Role == ChatRole.User && string.IsNullOrEmpty(m.AuthorName))
                    .TakeLast(1),
                CitationsPrompt = "Use the current city context, session summaries, and tools to answer accurately. Do not output raw SourceDocLink IDs."
            },
            loggerFactory);
    }
}
