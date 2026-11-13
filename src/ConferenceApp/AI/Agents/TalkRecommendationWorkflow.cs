using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

/// <summary>
/// Multi-turn group chat workflow for recommending sessions:
/// Turn 1: Concierge clarifies/confirms topics (using saved favourites as a starting point).
/// Turn 2: Concierge -> DepthAdvocate -> BreadthAdvocate -> PracticalAdvocate -> Moderator.
/// </summary>
public static class TalkRecommendationWorkflow
{
    public static IReadOnlyList<string> Utterances { get; } =
    [
        "Can you suggest some talks for me?",
        "What sessions or talks do you recommend I attend?",
        "Help me pick or choose which talks to go to",
        "Plan my day",
        "Recommend talks for me",
        "Debate: Help me pick talks"
    ];

    public static Workflow Create(IServiceProvider services)
    {
        var localClient = services.GetRequiredService<IChatClient>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var userContext = services.GetRequiredKeyedService<AIContextProvider>("user");
        var availableSessionsContext = services.GetRequiredKeyedService<AIContextProvider>("available-sessions");

        var concierge = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Concierge",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are Concierge, hosting the conference session recommendation
                        panel.

                        If you have NOT yet asked the attendee to confirm their topics:
                        - If Saved Favourites is "None yet" and the attendee did not
                          name specific topics, ask what topics they are interested in
                          (such as AI, Cloud, Data, Mobile, Web, or IoT).
                        - If they have Saved Favourites or named topics, mention only
                          the general topic category (such as Mobile or AI—do NOT quote
                          full session titles) and ask if they want to focus on those
                          or explore any other topics today.
                        Keep this to 1 or 2 short sentences and do NOT pitch talks yet.

                        If the attendee is replying to confirm or choose their topics:
                        - Output ONLY these two short lines:
                          Confirmed topics: <topics the attendee wants>
                          Avoid topics: <topics the attendee dislikes, or None>
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 140
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

                        Pick 1 talk from the candidate sessions in context that dives
                        deepest into the attendee's Confirmed topics (never an Avoid
                        topic) in 2 short sentences. State its exact time and title.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 160
                },
                AIContextProviders = [availableSessionsContext]
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

                        Pick 1 DIFFERENT talk from the candidate sessions in context
                        than DepthAdvocate's pick (never an Avoid topic) that broadens
                        the attendee's perspective in 2 short sentences. State its
                        exact time and title.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 160
                },
                AIContextProviders = [availableSessionsContext]
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

                        Pick 1 DIFFERENT hands-on or practical talk from the candidate
                        sessions in context than both DepthAdvocate and BreadthAdvocate
                        (never an Avoid topic) in 2 short sentences. State its exact
                        time and title.
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 160
                },
                AIContextProviders = [availableSessionsContext]
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
                        You are Moderator wrapping up the recommendation panel.

                        Summarize the 3 recommended talks from DepthAdvocate,
                        BreadthAdvocate, and PracticalAdvocate so the attendee can
                        decide which ones to attend. Output ONLY 3 short bullet lines
                        (one line per talk: "- HH:mm – HH:mm: Exact Title — short reason").
                        """,
                    Temperature = 0,
                    MaxOutputTokens = 300
                },
                AIContextProviders = [availableSessionsContext]
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
                    // run all 5 participants (Concierge -> Depth -> Breadth -> Practical -> Moderator).
                    var messages = history.ToList();
                    var lastUserIndex = messages.FindLastIndex(m =>
                        m.Role == ChatRole.User && string.IsNullOrEmpty(m.AuthorName));
                    var userConfirmedAfterConcierge = lastUserIndex > 0 &&
                        messages.Take(lastUserIndex).Any(m => m.AuthorName == "Concierge");

                    if (!userConfirmedAfterConcierge)
                        return ValueTask.FromResult(manager.IterationCount >= 1);

                    return ValueTask.FromResult(manager.IterationCount >= agents.Count);
                }))
            .AddParticipants(concierge, depthAdvocate, breadthAdvocate, practicalAdvocate, moderator)
            .WithName("TalkRecommendationWorkflow")
            .Build();
    }
}
