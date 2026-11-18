using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

// Generic conference assistant for fast everyday actions (times, rooms, speaker bios, and managing favourites).
public static class ConferenceAssistantAgent
{
    public static AIAgent Create(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = services.GetRequiredKeyedService<AIContextProvider>("user");
        var scheduleContext = services.GetRequiredKeyedService<AIContextProvider>("schedule");
        var ragContext = services.GetRequiredKeyedService<AIContextProvider>("rag");

#if WINDOWS
        loggerFactory.CreateLogger("ConferenceApp.AI.ConferenceAssistantAgent")
            .LogInformation("[AI Chat] Windows assistant uses context-only answers; function tools are unavailable.");
#endif

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
                        schedule, and RAG session summaries.

                        The city's full schedule of times, rooms, and titles (plus the
                        user's Saved Favourites) is already in your context, so you can
                        answer schedule, favourite, and time-clash questions immediately.
                        """ +
#if WINDOWS
                        """

                        You cannot call tools on Windows. Use only the supplied context.
                        If Saved Favourites is "None yet", tell the user they haven't
                        saved any favourite sessions yet and can tap Save on the Agenda.
                        You cannot add or remove favourites, navigate, or fetch full
                        session abstracts or speaker biographies. Never claim to have
                        performed these actions or invent missing details. Explain the
                        limitation briefly and direct the user to the Agenda or Speakers
                        page to view details, save talks, or open a profile manually.
                        """,
#else
                        """

                        If Saved Favourites is "None yet", tell the user they haven't
                        saved any favourite sessions yet and can tap ☆ Save on the
                        Agenda or ask you to add one.

                        Call get_session when you need a talk's full abstract,
                        get_speaker for a speaker's biography, or get_favorites,
                        add_favorite, and remove_favorite to manage saved talks.
                        When the user asks to add or remove all sessions for a topic
                        (such as Mobile or Gaming), call search_sessions first (or use
                        the [Topics] tags in the schedule) and then call add_favorite
                        or remove_favorite for each matching talk.

                        Call navigate_to_session, navigate_to_speaker, or
                        navigate_to_page when the user asks to open, go to, or navigate
                        to a session, speaker profile, tab, or city.
                        Never announce tool calls in text—either invoke the tool or
                        answer directly.
                        """,
#endif
                    Temperature = 0,
                    Tools =
#if WINDOWS
                        null
#else
                    [
                        .. ConferenceDataToolContext.Default.Tools,
                        .. FavoritesToolContext.Default.Tools,
                        .. NavigationToolContext.Default.Tools
                    ]
#endif
                },
                AIContextProviders = [userContext, scheduleContext, ragContext]
            },
            loggerFactory,
            services);
    }
}
