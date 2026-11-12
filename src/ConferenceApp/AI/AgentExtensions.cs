using ConferenceApp.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public static class AgentExtensions
{
    private const int TopRagSessions = 3;

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
        var ragProvider = CreateRagProvider(services, loggerFactory);

        // 1. Default Q&A and tool-calling assistant agent (with RAG + source-generated tools).
        var assistantAgent = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "ConferenceAssistant",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Temperature = 0,
                    MaxOutputTokens = 700,
                    Tools = [.. ConferenceToolContext.Default.Tools]
                },
                AIContextProviders = [ragProvider]
            },
            loggerFactory,
            services);

        // 2. Chained sequential workflow for "Plan my day": PreferencesAgent -> ScheduleAgent.
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
                        Build the complete conflict-free schedule of talks from CURRENT SCHEDULE that fit the
                        Preferences line and fall between Arrive and Leave.
                        Never pick two talks with overlapping times.
                        List every chosen talk as a bullet with its time, exact title from CURRENT SCHEDULE,
                        room, and one short sentence on why it fits.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 700
                },
                AIContextProviders = [ragProvider]
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

    private static TextSearchProvider CreateRagProvider(IServiceProvider services, ILoggerFactory loggerFactory)
    {
        var database = services.GetRequiredService<ConferenceDatabase>();
        var search = services.GetRequiredService<SearchService>();
        var tools = services.GetRequiredService<ConferenceAssistantTools>();
        var logger = loggerFactory.CreateLogger("ConferenceApp.AI.AgentExtensions");

        return new TextSearchProvider(
            async (query, cancellationToken) =>
            {
                var trimmed = string.Join(" ", query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (string.IsNullOrWhiteSpace(trimmed))
                    return [];

                var city = tools.CurrentCity;
                var matches = (await search.SearchSessionsAsync(city.Id, trimmed))
                    .Where(session => session.IsTalk)
                    .Take(TopRagSessions)
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
