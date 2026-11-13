using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using ConferenceApp.AI;
using ConferenceApp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.Services;

public class ChatService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    [FromKeyedServices("assistant")] IChatClient chatClient,
    ILogger<ChatService> logger)
{
    public ObservableCollection<ChatItem> Messages { get; } = [];

    public IReadOnlyList<string> Suggestions { get; } =
    [
        "When is Matthew's talk?",
        "Prepare me for Matthew's talk: takeaways and questions",
        "Can you suggest some talks for me?",
        "Do my favourites clash?"
    ];

    public async Task<string?> GetUnavailableReasonAsync(CancellationToken cancellationToken = default) =>
        await ChatExtensions.GetModelAvailabilityAsync(cancellationToken) switch
        {
            ChatModelAvailability.Available => null,
            ChatModelAvailability.Disabled => "On-device AI is turned off. Enable it in your device settings to chat locally. No cloud service is used.",
            ChatModelAvailability.NotReady or ChatModelAvailability.Downloading =>
                "The on-device model is still getting ready. Try again after it finishes downloading.",
            _ => "On-device chat isn't available on this device right now. You can still browse the agenda and favourites. No cloud service is used."
        };

    public async IAsyncEnumerable<string> SendAsync(
        IReadOnlyList<ChatItem> messages,
        ChatItem reply,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (await GetUnavailableReasonAsync(cancellationToken) is { } reason)
        {
            yield return reason;
            yield break;
        }

        reply.Progress = "Thinking on device...";

        // Build conversation history from prior chat turns.
        var history = new List<ChatMessage>();
        foreach (var message in messages)
        {
            if (!message.IsUser && message.ResponseMessages.Count > 0)
                history.AddRange(message.ResponseMessages);
            else
                history.Add(new(message.IsUser ? ChatRole.User : ChatRole.Assistant, message.Text));
        }

        var receivedText = false;
        var isDebateFollowUp = history.LastOrDefault(m =>
            m.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.AuthorName))?.AuthorName == "Concierge";
        string? activeSpeaker = null;
        var bufferedQuestions = new System.Text.StringBuilder();
        var updates = new List<ChatResponseUpdate>();

        // Stream from the keyed "assistant" IChatClient (routes to ConferenceAssistant, SessionDeepDive, or TalkRecommendations).
        await foreach (var update in chatClient.GetStreamingResponseAsync(history, cancellationToken: cancellationToken))
        {
            // Step 1 of SessionDeepDiveWorkflow gathers the session abstract and speaker bio for the parallel analysts.
            if (update.AuthorName == "SessionResearcher")
            {
                reply.Progress = "SessionResearcher: reading session abstract and speaker bio...";
                continue;
            }

            // On Turn 2 of TalkRecommendations (after the user confirmed their topics), Concierge's 2-line
            // summary and Timekeeper's filtered session list are internal handoff state for the panelists.
            if (update.AuthorName == "Concierge" && isDebateFollowUp)
            {
                reply.Progress = "Concierge: locking in your topics and time window...";
                continue;
            }

            if (update.AuthorName == "Timekeeper")
            {
                reply.Progress = "Timekeeper: filtering talks to your time window...";
                continue;
            }

            if (update.AuthorName is "TakeawaysAgent" or "QuestionsCoachAgent"
                && !receivedText)
            {
                reply.Progress = "Running TakeawaysAgent & QuestionsCoachAgent in parallel...";
            }

            if (update.AuthorName is "DepthAdvocate" or "BreadthAdvocate" or "PracticalAdvocate" or "Moderator"
                && !receivedText)
            {
                reply.Progress = $"{update.AuthorName}: preparing argument...";
            }

            // When TakeawaysAgent and QuestionsCoachAgent run concurrently via BuildConcurrent,
            // stream TakeawaysAgent live and buffer QuestionsCoachAgent so their sentences don't interleave.
            if (update.AuthorName == "QuestionsCoachAgent")
            {
                updates.Add(update);
                if (!string.IsNullOrEmpty(update.Text))
                    bufferedQuestions.Append(update.Text);
                continue;
            }

            updates.Add(update);
            if ((update.Role is null || update.Role == ChatRole.Assistant) && !string.IsNullOrEmpty(update.Text))
            {
                if (!receivedText)
                {
                    receivedText = true;
                    reply.Progress = "";
                }

                // Label each multi-agent workflow participant as they take the floor.
                if (update.AuthorName is "TakeawaysAgent" or "DepthAdvocate" or "BreadthAdvocate" or "PracticalAdvocate" or "Timekeeper" or "Moderator"
                    && update.AuthorName != activeSpeaker)
                {
                    var label = update.AuthorName == "TakeawaysAgent" ? "Key Takeaways" : update.AuthorName;
                    var prefix = activeSpeaker is null
                        ? $"**{label}:** "
                        : $"\n\n**{label}:** ";
                    activeSpeaker = update.AuthorName;
                    yield return prefix;
                }

                yield return update.Text;
            }
        }

        if (bufferedQuestions.Length > 0)
        {
            if (!receivedText)
                reply.Progress = "";

            var prefix = activeSpeaker is null
                ? "**Things to Think About & Ask:** "
                : "\n\n**Things to Think About & Ask:** ";
            yield return prefix + bufferedQuestions;
        }

        reply.ResponseMessages = updates
            .ToChatResponse()
            .Messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .ToArray();
        await AddReferencedSessionsAsync(reply);

        logger.LogInformation("[AI Chat] Completed reply ({Length} chars, {Cards} session cards).",
            reply.Text.Length, reply.Sessions.Count);
    }

    // Attaches tappable session cards under the assistant reply for any talk mentioned in the response.
    private async Task AddReferencedSessionsAsync(ChatItem reply)
    {
        var db = await dbProvider.GetDatabaseAsync();
        var city = await preferences.GetSelectedCityAsync();

        var referenced = db.Sessions
            .Where(s => s.CityId == city.Id && s.IsTalk && (
                reply.Text.Contains(s.Title, StringComparison.OrdinalIgnoreCase) ||
                reply.Text.Contains(s.Id, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        foreach (var session in referenced)
        {
            if (reply.Sessions.Any(s => s.Id == session.Id))
                continue;

            reply.Sessions.Add(session);
        }
    }

    internal static bool IsDayPlanRequest(string request) =>
        request.Equals("Plan my day", StringComparison.OrdinalIgnoreCase) ||
        request.StartsWith("Plan my day:", StringComparison.OrdinalIgnoreCase) ||
        request.StartsWith("Plan my day ", StringComparison.OrdinalIgnoreCase);
}
