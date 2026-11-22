using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

// Multi-turn group chat workflow for recommending sessions:
// Turn 1: Concierge clarifies/confirms topics (using saved favourites as a starting point).
// Turn 2: Concierge confirms topics, then two rounds of DepthAdvocate <-> AlternativeAdvocate
//         (one talk per pitch), then Moderator wraps up the shortlist.
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
                          exploring today (such as AI, Cloud, Mobile, Web, Data, or
                          Architecture).
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
                    Temperature = 0
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
                        You are DepthAdvocate on the conference panel. You champion the
                        talks that go deepest into the attendee's Confirmed topics.

                        Pitch exactly ONE NEW talk from the candidate sessions in context.
                        NEVER recommend a session that has already been mentioned in this
                        conversation, no matter who mentioned it. If you already pitched
                        a talk, your next pitch must be a different talk.
                        - Prefer a talk whose [Topics] include a Confirmed topic, and
                          never one with an Avoid topic.
                        - If every Confirmed-topic talk has already been mentioned, pick
                          the closest related new talk and say it is a related pick.
                        Output 1 or 2 short sentences: the exact time and title, then
                        why it is worth attending.
                        """,
                    Temperature = 0.7f
                },
                AIContextProviders = [availableSessionsContext]
            },
            loggerFactory,
            services);

        var alternativeAdvocate = localClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "AlternativeAdvocate",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = """
                        You are AlternativeAdvocate on the conference panel. You react to
                        the previous pitch and offer a different option.

                        Pitch exactly ONE NEW talk from the candidate sessions in context.
                        NEVER recommend a session that has already been mentioned in this
                        conversation, no matter who mentioned it (including you).
                        - Prefer a different angle on a Confirmed topic, or a strong
                          related talk. Never an Avoid topic.
                        Output 1 or 2 short sentences: briefly react to the previous
                        pitch, then give the exact time and title of your alternative
                        and why it is worth attending.
                        """,
                    Temperature = 0.7f
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

                        List the distinct talks pitched by DepthAdvocate and
                        AlternativeAdvocate in chronological order. Output ONLY bullet
                        lines ("- HH:mm – HH:mm: Exact Title — short reason").
                        """,
                    Temperature = 0
                },
                AIContextProviders = [availableSessionsContext]
            },
            loggerFactory,
            services);

        return AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(_ => new PanelGroupChatManager(
                concierge,
                [concierge, depthAdvocate, alternativeAdvocate, depthAdvocate, alternativeAdvocate, moderator]))
            .AddParticipants(concierge, depthAdvocate, alternativeAdvocate, moderator)
            .WithName("TalkRecommendationWorkflow")
            .Build();
    }

    // Turn 1: only Concierge speaks (to ask for topics), then the chat pauses for the attendee.
    // Turn 2: once the attendee has replied, the panel follows a fixed two-round script.
    private sealed class PanelGroupChatManager(AIAgent concierge, IReadOnlyList<AIAgent> panel) : GroupChatManager
    {
        protected override ValueTask<AIAgent> SelectNextAgentAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(HasConfirmedAfterConcierge(history) ? panel[IterationCount] : concierge);

        protected override ValueTask<bool> ShouldTerminateAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(IterationCount >= (HasConfirmedAfterConcierge(history) ? panel.Count : 1));
    }

    // Turn 2 continues only once a new user message arrives after Concierge's question.
    private static bool HasConfirmedAfterConcierge(IReadOnlyList<ChatMessage> messages)
    {
        for (var i = messages.Count - 1; i > 0; i--)
        {
            if (messages[i].Role == ChatRole.User && string.IsNullOrEmpty(messages[i].AuthorName))
                return messages.Take(i).Any(m => m.AuthorName == "Concierge");
        }

        return false;
    }
}
