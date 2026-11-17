using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ConferenceApp.AI;

// Registers AIContextProviders and the keyed "assistant" IChatClient.
public static class AgentExtensions
{
    public static MauiAppBuilder AddAgents(this MauiAppBuilder builder)
    {
        // Register composable AIContextProviders for user state, city schedule, and RAG.
        builder.Services.AddKeyedSingleton<AIContextProvider, UserContextProvider>("user");
        builder.Services.AddKeyedSingleton<AIContextProvider, ScheduleContextProvider>("schedule");
        builder.Services.AddKeyedSingleton<AIContextProvider>("rag", (services, _) => RagContextProvider.Create(services));

        // Register the keyed "assistant" IChatClient backed by ConferenceAssistantAgent.
        builder.Services.AddKeyedChatClient("assistant", services =>
            ConferenceAssistantAgent.Create(services).AsIChatClient());

        return builder;
    }
}
