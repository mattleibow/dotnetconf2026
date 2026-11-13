using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

/// <summary>
/// Multi-turn group chat workflow:
/// First turn: Concierge clarifies/confirms topics (using saved favourites as a starting point) &amp; time window.
/// Follow-up turn: Concierge -> Timekeeper (filters talks by time window via list_sessions) -> Depth -> Breadth -> Practical -> Moderator.
/// </summary>
public static class TalkRecommendationWorkflow
{
    public static IReadOnlyList<string> Utterances { get; } =
    [
        "Can you suggest some talks for me?",
        "What sessions or talks do you recommend I attend?",
        "Help me pick or choose which talks to go to",
        "Plan my day",
        "Plan my day: I'm into mobile and AI, leaving at 3",
        "Debate: Help me pick talks"
    ];

    public static Workflow Create(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = services.GetRequiredKeyedService<AIContextProvider>("user");
        var dataTools = services.GetRequiredService<ConferenceDataTools>();

        // Ensure Timekeeper's message in the group chat history includes the full untruncated
        // list_sessions tool output so all 3 advocates see every open time slot.
        var timekeeperClient = localClient
            .AsBuilder()
            .Use(
                getResponseFunc: null,
                getStreamingResponseFunc: (messages, options, innerClient, cancellationToken) =>
                    AppendListedSessionsAsync(innerClient.GetStreamingResponseAsync(messages, options, cancellationToken), dataTools))
            .Build();

        var concierge = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Concierge",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are Concierge, hosting the conference talk recommendation
                        and debate panel.

                        If you have NOT yet asked the attendee to confirm their topics:
                        - If Saved Favourites is "None yet" and the attendee did not
                          name specific topics, ask what topics they are interested in
                          today (such as AI, Cloud, Data, Mobile, Web, or IoT) and
                          whether they need to arrive late or leave early.
                        - If they have Saved Favourites or named topics, mention only
                          the general topic category (such as Mobile or AI—do NOT quote
                          full session titles or time slots) and ask if they want to
                          explore other topics or have any arrival/departure time limits.
                        Keep this to 1 or 2 short sentences and do NOT pitch talks yet.

                        If the attendee is replying to confirm or choose their topics:
                        - Output ONLY these three short lines in 24-hour time.
                          IMPORTANT: Always use 08:00 to 17:00 for Time window unless
                          the attendee explicitly stated an arrival or departure time
                          in their message:
                          Confirmed topics: <topics the attendee wants>
                          Avoid topics: <topics the attendee dislikes, or None>
                          Time window: <HH:mm> to <HH:mm>
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 150
                },
                AIContextProviders = [userContext]
            },
            loggerFactory,
            services);

        var timekeeper = timekeeperClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Timekeeper",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are Timekeeper on the conference panel.

                        Read the Time window line from Concierge (default 08:00 to 17:00)
                        and call list_sessions with that exact start and end time.
                        Then reply with one short line confirming the time window.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 120,
                    Tools = [.. ConferenceDataToolContext.Default.Tools]
                }
            },
            loggerFactory,
            services);

        var depthAdvocate = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "DepthAdvocate",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are DepthAdvocate on the conference panel.

                        From ONLY the open candidate talks listed by Timekeeper (never
                        a talk in an Avoid topic), pitch 1 session that dives deepest
                        into the attendee's Confirmed topics in 2 short sentences.
                        State its exact time and title.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 160
                }
            },
            loggerFactory,
            services);

        var breadthAdvocate = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "BreadthAdvocate",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are BreadthAdvocate on the conference panel.

                        From ONLY the open candidate talks listed by Timekeeper, pick
                        1 session in a DIFFERENT time slot than DepthAdvocate's pick
                        (and never in an Avoid topic) to broaden the attendee's day in
                        2 short sentences. State its exact time and title.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 160
                }
            },
            loggerFactory,
            services);

        var practicalAdvocate = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "PracticalAdvocate",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are PracticalAdvocate on the conference panel.

                        From ONLY the open candidate talks listed by Timekeeper, pick
                        1 hands-on or code-first session in a DIFFERENT time slot than
                        both DepthAdvocate and BreadthAdvocate (and never in an Avoid
                        topic) in 2 short sentences. State its exact time and title.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 160
                }
            },
            loggerFactory,
            services);

        var moderator = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Moderator",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are Moderator wrapping up the panel debate.

                        Present a chronological, conflict-free lineup of the 3 distinct
                        time-slot picks from DepthAdvocate, BreadthAdvocate, and
                        PracticalAdvocate. Output ONLY 3 short bullet lines (one line
                        per talk: "- HH:mm – HH:mm: Exact Title") with no extra paragraphs.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 300
                }
            },
            loggerFactory,
            services);

        return AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(agents => new RoundRobinGroupChatManager(
                agents,
                (manager, history, _) =>
                {
                    // Before the panel debates, pause after Concierge (Iteration 1) so the user
                    // can confirm or adjust their topics. Once the user replies to Concierge,
                    // run all 6 participants (Concierge -> Timekeeper -> Depth -> Breadth -> Practical -> Moderator).
                    var messages = history.ToList();
                    var lastUserIndex = messages.FindLastIndex(m =>
                        m.Role == ChatRole.User && string.IsNullOrEmpty(m.AuthorName));
                    var userConfirmedAfterConcierge = lastUserIndex > 0 &&
                        messages.Take(lastUserIndex).Any(m => m.AuthorName == "Concierge");

                    if (!userConfirmedAfterConcierge)
                        return ValueTask.FromResult(manager.IterationCount >= 1);

                    return ValueTask.FromResult(manager.IterationCount >= agents.Count);
                }))
            .AddParticipants(concierge, timekeeper, depthAdvocate, breadthAdvocate, practicalAdvocate, moderator)
            .WithName("TalkRecommendationWorkflow")
            .Build();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> AppendListedSessionsAsync(
        IAsyncEnumerable<ChatResponseUpdate> updates,
        ConferenceDataTools dataTools)
    {
        await foreach (var update in updates)
            yield return update;

        if (!string.IsNullOrWhiteSpace(dataTools.LastListedSessions))
        {
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                $"\nOpen candidate talks in time window:\n{dataTools.LastListedSessions}")
            {
                AuthorName = "Timekeeper"
            };
        }
    }
}
