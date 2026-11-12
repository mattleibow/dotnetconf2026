using ConferenceApp.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public static class AIExtensions
{
    private static readonly IReadOnlyList<string> DayPlanUtterances =
    [
        "Plan my day",
        "Plan my day: I'm into mobile and AI, leaving at 3",
        "Build me a conflict-free schedule for today",
        "Create a full day itinerary of sessions with no clashes"
    ];

    public static MauiAppBuilder AddConferenceAI(this MauiAppBuilder builder)
    {
        // 1. Semantic embeddings: MiniLM ONNX (384-dim) runs on every platform, wrapped with Microsoft.Extensions.AI logging.
        // On Apple, you can also compare against the built-in NLEmbeddingGenerator:
        // builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
        //     sp => new NLEmbeddingGenerator(sp.GetRequiredService<ILoggerFactory>()).AsBuilder().UseLogging(sp.GetRequiredService<ILoggerFactory>()).Build());
        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            ActivatorUtilities.CreateInstance<MiniLmEmbeddingGenerator>(sp)
                .AsBuilder()
                .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                .Build());

        // 2. On-device chat client across Apple (AppleIntelligenceChatClient), Android (GeminiNanoChatClient),
        // and Windows (WindowsAIChatClient) from Microsoft.Maui.Essentials.AI, wrapped with logging.
        builder.Services.AddTransient<IChatClient>(CreateLocalChatClient);

        // 3. Data ingestion, semantic search, tools, RAG middleware, sequential workflow & semantic router.
        builder.Services.AddSingleton<SessionEnrichmentService>();
        builder.Services.AddSingleton<SearchService>();
        builder.Services.AddScoped<ConferenceAssistantTools>();
        builder.Services.AddScoped<SemanticRoutingChatClient>(CreateAssistantRouter);
        builder.Services.AddSingleton<ChatService>();

        return builder;
    }

    public static async Task<ChatModelAvailability> GetModelAvailabilityAsync(CancellationToken cancellationToken = default)
    {
#if IOS || MACCATALYST
        if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
            return (await AppleIntelligenceChatClient.GetModelStatusAsync(cancellationToken)).Availability;
#elif ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            return (await GeminiNanoChatClient.GetModelStatusAsync(cancellationToken)).Availability;
#elif WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
            return (await WindowsAIChatClient.GetModelStatusAsync(cancellationToken)).Availability;
#endif
        await Task.CompletedTask;
        return ChatModelAvailability.NotSupported;
    }

    private static IChatClient CreateLocalChatClient(IServiceProvider services)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
#if IOS || MACCATALYST
        if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
        {
            return new AppleIntelligenceChatClient(loggerFactory: loggerFactory, functionInvocationServices: services)
                .AsBuilder()
                .UseLogging(loggerFactory)
                .Build();
        }
#elif ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return new GeminiNanoChatClient()
                .AsBuilder()
                .UseFunctionInvocation(loggerFactory)
                .UseLogging(loggerFactory)
                .Build();
        }
#elif WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            return new WindowsAIChatClient()
                .AsBuilder()
                .UseFunctionInvocation(loggerFactory)
                .UseLogging(loggerFactory)
                .Build();
        }
#endif
        throw new PlatformNotSupportedException("On-device chat is not supported on this OS version.");
    }

    private static SemanticRoutingChatClient CreateAssistantRouter(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();

        // Agent Framework RAG middleware: injects the top 3 matching session abstracts before each turn.
        var ragProvider = SessionRagProvider.Create(
            services.GetRequiredService<ConferenceDatabase>(),
            services.GetRequiredService<SearchService>(),
            services.GetRequiredService<ConferenceAssistantTools>(),
            loggerFactory);

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
}
