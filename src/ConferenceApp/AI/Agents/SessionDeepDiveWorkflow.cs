using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

// SessionResearcher only gathers session and speaker data (with tools and context); then TakeawaysAgent and
// QuestionsCoachAgent run in parallel with no tools or context, working purely from the researcher's output.
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
                        You are a researcher. Your ONLY job is to gather data for the
                        agents that run after you. Do not analyse the session, give
                        takeaways, suggest questions, or add any commentary.

                        Find the specific session the attendee is asking about from the
                        city schedule in context. Call get_session to read its full
                        abstract and call get_speaker to read the speaker's biography.

                        Output only the raw facts, copied without summarizing:
                        Session: exact title, time, room, and full abstract.
                        Speaker: name, tagline, and full biography.
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
                        Using only the session and speaker details collected by
                        SessionResearcher, write 3 concise bullet points ("- ")
                        explaining what to expect in this session and the key technical
                        takeaways for a developer. Include the exact session title and
                        time in the first bullet. Keep each bullet to 1 or 2 short
                        sentences. Do not include an introductory sentence.
                        """,
                    Temperature = 0
                }
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
                        Using only the session and speaker details collected by
                        SessionResearcher, suggest 2 thoughtful things to think about
                        during the talk and 2 smart questions the attendee can ask the
                        speaker during Q&A. Use bullet points ("- ") and keep each
                        point to 1 or 2 short sentences. Do not include an introductory
                        sentence.
                        """,
                    Temperature = 0
                }
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
