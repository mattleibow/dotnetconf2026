using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

/// <summary>
/// Generic conference assistant for fast everyday actions
/// (times, rooms, speaker bios, and managing favourites).
/// </summary>
public static class ConferenceAssistantAgent
{
    public static AIAgent Create(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = new UserContextProvider(services);
        var scheduleContext = new ScheduleContextProvider(services);
        var ragContext = RagContextProvider.Create(services);

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
}
