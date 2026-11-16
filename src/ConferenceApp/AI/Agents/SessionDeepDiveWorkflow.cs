using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

/// <summary>
/// Sequential -> Concurrent fan-out workflow:
/// SessionResearcher gathers the session abstract and speaker bio via tools, then
/// TakeawaysAgent (what to expect) and QuestionsCoachAgent (questions to ask) run in parallel.
/// </summary>
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

                        Summarize the session title, time, room, speaker bio, and key
                        abstract points in a concise factual brief for the analysts.
                        """,
                    Temperature = 0,
                    Tools = [.. ConferenceDataToolContext.Default.Tools]
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
                        Using SessionResearcher's brief, write 3 concise bullet points
                        ("- ") explaining what to expect in this session and the key
                        technical takeaways for a developer. Include the exact session
                        title and time in the first bullet. Keep each bullet to 1 or 2
                        short sentences. Do not include an introductory sentence.
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
                        Using SessionResearcher's brief, suggest 2 thoughtful things to
                        think about during the talk and 2 smart questions the attendee
                        can ask the speaker during Q&A. Use bullet points ("- ") and
                        keep each point to 1 or 2 short sentences. Do not include an
                        introductory sentence.
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
