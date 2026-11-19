using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

// SessionResearcher collects the full session and speaker details, then TakeawaysAgent and QuestionsCoachAgent run in parallel.
public static class SessionDeepDiveWorkflow
{
    public static IReadOnlyList<string> Utterances { get; } =
    [
        "Give me a deep dive on this session",
        "Prepare me for this talk: takeaways and questions to ask",
        "What are the key takeaways and questions to ask the speaker?",
        "Give me a deep dive, what to expect, and questions to ask in Q&A"
    ];

    public static Workflow Create(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = services.GetRequiredKeyedService<AIContextProvider>("user");
        var scheduleContext = services.GetRequiredKeyedService<AIContextProvider>("schedule");

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

                        Output the complete session details (exact title, time, room,
                        and full abstract) and complete speaker details (name, tagline,
                        and full biography) without summarizing or omitting details, so
                        the downstream agents have all the information to work with.
                        """,
                    Tools = [.. ConferenceDataToolContext.Default.Tools],
                    Temperature = 0
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
                        Using the session and speaker details collected by
                        SessionResearcher, write 3 concise bullet points ("- ")
                        explaining what to expect in this session and the key technical
                        takeaways for a developer. Include the exact session title and
                        time in the first bullet. Keep each bullet to 1 or 2 short
                        sentences. Do not include an introductory sentence.
                        """,
                    Temperature = 0
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
                        Using the session and speaker details collected by
                        SessionResearcher, suggest 2 thoughtful things to think about
                        during the talk and 2 smart questions the attendee can ask the
                        speaker during Q&A. Use bullet points ("- ") and keep each
                        point to 1 or 2 short sentences. Do not include an introductory
                        sentence.
                        """,
                    Temperature = 0
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
}
