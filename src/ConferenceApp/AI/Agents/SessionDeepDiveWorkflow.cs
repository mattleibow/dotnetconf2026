using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

/// <summary>
/// Route 2 (Sequential -> Concurrent Fan-Out):
/// Step 1 (SessionResearcher) gathers the session abstract and speaker bio via tools;
/// Step 2 fans out concurrently to TakeawaysAgent (what to expect) and QuestionsCoachAgent (questions to ask).
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
        var userContext = new UserContextProvider(services);
        var scheduleContext = new ScheduleContextProvider(services);

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
                    MaxOutputTokens = 350,
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
                        explaining what to expect in this session and the key technical
                        takeaways for a developer. Include the exact session title and
                        time in the first bullet.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 240
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
                        can ask the speaker during Q&A.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 240
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
