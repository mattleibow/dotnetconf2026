using Microsoft.Agents.AI;
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

        return assistantClient;
    }
}
