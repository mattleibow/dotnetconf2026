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
        var contextProvider = CreateContextProvider(services, loggerFactory);

        // 1. General assistant agent: uses RAG summaries + user context + source-generated tools.
        var assistantAgent = localClient.AsAIAgent(
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
                AIContextProviders = [contextProvider]
            },
            loggerFactory,
            services);

        // 2. Sequential "Plan my day" workflow: PreferencesAgent -> ScheduleAgent.
        var preferencesAgent = localClient.AsAIAgent(
            name: "PreferencesAgent",
            instructions: """
                Do NOT list or pick any talks or sessions.
                Output ONLY one short line in this exact format:
                "Preferences — Topics: <topics>; Arrive: <HH:mm>; Leave: <HH:mm>. Now build the full schedule."
                Use 24-hour daytime ("leaving at 3" means 15:00; default Arrive 08:00, Leave 17:00).
                """,
            loggerFactory: loggerFactory,
            services: services);

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
                AIContextProviders = [contextProvider]
            },
            loggerFactory,
            services);

        var dayPlanWorkflow = AgentWorkflowBuilder.BuildSequential(
            "DayPlanWorkflow",
            [preferencesAgent, scheduleAgent]);

        // 3. Semantic router: uses our ONNX MiniLM embeddings to route schedule-planning prompts
        // to the sequential workflow, while keeping general Q&A and tool calls on the assistant agent.
        return new SemanticRoutingChatClient(
            services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
            new Dictionary<IChatClient, IReadOnlyList<string>>
            {
                [dayPlanWorkflow.AsAIAgent(name: "DayPlanner").AsIChatClient(allowNonChatClientAgents: true)] = DayPlanUtterances
            },
            defaultClient: assistantAgent.AsIChatClient(),
            scoreThreshold: 0.60f,
            topK: 1,
            leaveOpen: true);
    }

    // Injects the current city, venue, saved favourites, and top RAG session summaries (title + topic + 1-line summary)
    // before each turn. For full multi-paragraph abstracts, the model calls the get_session tool on demand.
    private static TextSearchProvider CreateContextProvider(IServiceProvider services, ILoggerFactory loggerFactory)
    {
        var database = services.GetRequiredService<ConferenceDatabase>();
        var preferences = services.GetRequiredService<UserPreferencesService>();
        var search = services.GetRequiredService<SearchService>();
        var tools = services.GetRequiredService<ConferenceAssistantTools>();
        var logger = loggerFactory.CreateLogger("ConferenceApp.AI.AgentExtensions");

        return new TextSearchProvider(
            async (query, cancellationToken) =>
            {
                var city = await preferences.GetSelectedCityAsync(database);
                var conference = await database.GetConferenceAsync();
                var venue = conference.Venues.First(v => v.Id == city.VenueId);
                var allCitySessions = await database.GetSessionsAsync(city.Id);
                var favoriteTitles = allCitySessions
                    .Where(s => preferences.IsFavorite(s.Id))
                    .Select(s => $"{s.Time} {s.Title}")
                    .ToArray();

                var results = new List<TextSearchProvider.TextSearchResult>
                {
                    new()
                    {
                        SourceName = $"Current Event Context ({city.Name})",
                        SourceLink = city.Id,
                        Text = $"""
                            Event: {conference.Name}
                            Selected City: {city.Name} | Date: {city.Date:dddd, yyyy-MM-dd}
                            Venue: {venue.Name} ({venue.Address})
                            Saved Favourites: {(favoriteTitles.Length > 0 ? string.Join("; ", favoriteTitles) : "None yet")}
                            """
                    }
                };

                var trimmed = string.Join(" ", query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    var matches = (await search.SearchSessionsAsync(city.Id, trimmed))
                        .Where(session => session.IsTalk)
                        .Take(TopRagSessions)
                        .ToList();

                    if (matches.Count > 0)
                    {
                        results.Add(new TextSearchProvider.TextSearchResult
                        {
                            SourceName = $"Top Semantic Matches for '{trimmed}'",
                            SourceLink = "rag-matches",
                            Text = await tools.FormatSessionsAsync(city, matches)
                        });
                    }

                    logger.LogInformation("[AI RAG] Injected {City} context + {Count} session summaries for '{Query}': {Titles}",
                        city.Name, matches.Count, trimmed, string.Join(" | ", matches.Select(s => s.Title)));
                }

                return results;
            },
            new TextSearchProviderOptions
            {
                SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
                SearchInputMessageFilter = messages => messages.Where(m => m.Role == ChatRole.User).TakeLast(2),
                CitationsPrompt = "Use the current city context, session summaries, and tools to answer accurately. Do not output raw SourceDocLink IDs."
            },
            loggerFactory);
    }
}
