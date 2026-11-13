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
        var userContext = new UserContextProvider(services);

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
                        - If they have Saved Favourites or named topics, mention those
                          topics (and any arrival/departure time they gave) as a
                          starting point, and ask if they are happy with that or want
                          to explore other topics too.
                        Keep this to 1 or 2 short sentences and do NOT pitch talks yet.

                        If the attendee is replying to confirm or choose their topics:
                        - Output ONLY these two short lines in 24-hour time (default
                          08:00 to 17:00; "before noon" means 08:00 to 12:00; "leaving
                          at 3" means 08:00 to 15:00):
                          Confirmed topics: <topics>
                          Time window: <HH:mm> to <HH:mm>
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 150
                },
                AIContextProviders = [userContext]
            },
            loggerFactory,
            services);

        var timekeeper = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Timekeeper",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are Timekeeper on the conference panel.

                        Read the Time window line from Concierge (for example 08:00 to
                        12:00) and call list_sessions with that exact start and end time.
                        Then output ONLY the matching talks returned by list_sessions
                        so the panel only debates talks that fit the attendee's schedule.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 450,
                    Tools = [.. ConferenceDataToolContext.Default.Tools]
                },
                AIContextProviders = [userContext]
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

                        From ONLY the talks listed by Timekeeper, pitch 1 or 2 sessions
                        that dive deepest into the attendee's Confirmed topics in 2
                        short sentences. Never mention talks outside Timekeeper's list.
                        Always use exact session titles and times.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContext]
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

                        From ONLY the talks listed by Timekeeper, pitch 1 or 2 sessions
                        from a DIFFERENT topic than DepthAdvocate's pick to expand the
                        attendee's horizons in 2 short sentences. Never mention talks
                        outside Timekeeper's list. Always use exact session titles and times.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContext]
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

                        From ONLY the talks listed by Timekeeper, pitch 1 or 2 hands-on,
                        code-first or demo-heavy sessions the attendee can apply right
                        away in 2 short sentences. Never mention talks outside
                        Timekeeper's list. Always use exact session titles and times.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 180
                },
                AIContextProviders = [userContext]
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

                        From ONLY the talks pitched by DepthAdvocate, BreadthAdvocate,
                        and PracticalAdvocate within Timekeeper's window, present a
                        conflict-free recommended lineup (one talk per time slot, with
                        exact times and titles) in 2 or 3 short sentences.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 200
                },
                AIContextProviders = [userContext]
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
}
