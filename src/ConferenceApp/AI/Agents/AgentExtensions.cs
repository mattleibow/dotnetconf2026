using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ConferenceApp.AI;

// Registers AIContextProviders and the keyed "assistant" IChatClient.
public static class AgentExtensions
{
    public static MauiAppBuilder AddAgents(this MauiAppBuilder builder)
    {
        // Register composable AIContextProviders.
        builder.Services.AddKeyedSingleton<AIContextProvider, UserContextProvider>("user");
        builder.Services.AddKeyedSingleton<AIContextProvider, ScheduleContextProvider>("schedule");
        builder.Services.AddKeyedSingleton<AIContextProvider, AvailableSessionsContextProvider>("available-sessions");
        builder.Services.AddKeyedSingleton<AIContextProvider>("rag", (services, _) => RagContextProvider.Create(services));

        // Register the keyed "assistant" IChatClient.
        builder.Services.AddKeyedChatClient("assistant", CreateAssistant);

        return builder;
    }

    private static IChatClient CreateAssistant(IServiceProvider services)
    {
        // Default assistant with the full day schedule in context + all tools.
        var assistantClient = ConferenceAssistantAgent.Create(services)
            .AsIChatClient();

        // Sequential + concurrent fan-out: Researcher -> parallel [TakeawaysAgent, QuestionsCoachAgent].
        var deepDiveClient = SessionDeepDiveWorkflow.Create(services)
            .AsAIAgent(name: "SessionDeepDive")
            .AsIChatClient(allowNonChatClientAgents: true);

        // Multi-turn group chat: Concierge -> Depth/Breadth/Practical -> Moderator.
        var recommendationClient = TalkRecommendationWorkflow.Create(services)
            .AsAIAgent(name: "TalkRecommendations")
            .AsIChatClient(allowNonChatClientAgents: true);

        // Semantic router: uses our ONNX MiniLM embeddings to pick the right agent or workflow per prompt.
        var semanticRouter = new SemanticRoutingChatClient(
            services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
            new Dictionary<IChatClient, IReadOnlyList<string>>
            {
                [deepDiveClient] = SessionDeepDiveWorkflow.Utterances,
                [recommendationClient] = TalkRecommendationWorkflow.Utterances
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
}
