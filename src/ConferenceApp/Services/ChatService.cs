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
    UserPreferencesService preferences,
    IServiceScopeFactory scopeFactory,
    ILogger<ChatService> logger)
{
    public ObservableCollection<ChatItem> Messages { get; } = [];

    public IReadOnlyList<string> Suggestions { get; } =
    [
        "When is Matthew's talk?",
        "What's on after lunch?",
        "Debate: AI vs Cloud talks",
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

        using var scope = scopeFactory.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<ConferenceAssistantTools>();
        var router = scope.ServiceProvider.GetRequiredService<SemanticRoutingChatClient>();

        tools.CurrentReply = reply;
        await MainThread.InvokeOnMainThreadAsync(() => reply.Progress = "Thinking on device...");

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
        var isDayPlan = false;
        string? activeDebateSpeaker = null;
        var updates = new List<ChatResponseUpdate>();

        // Stream from SemanticRoutingChatClient (routes to ConferenceAssistant, DayPlanWorkflow, or PanelDebateWorkflow).
        await foreach (var update in router.GetStreamingResponseAsync(history, cancellationToken: cancellationToken))
        {
            if (update.AuthorName == "PreferencesAgent")
            {
                isDayPlan = true;
                await MainThread.InvokeOnMainThreadAsync(() =>
                    reply.Progress = "PreferencesAgent: reading your interests and time window...");
                continue;
            }

            if (update.AuthorName == "ScheduleAgent" && !receivedText)
            {
                isDayPlan = true;
                await MainThread.InvokeOnMainThreadAsync(() =>
                    reply.Progress = "ScheduleAgent: building your conflict-free schedule...");
            }

            if (update.AuthorName is "PracticalBuilder" or "SystemArchitect" or "TrackModerator"
                && !receivedText)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    reply.Progress = $"{update.AuthorName}: preparing argument...");
            }

            updates.Add(update);
            if ((update.Role is null || update.Role == ChatRole.Assistant) && !string.IsNullOrEmpty(update.Text))
            {
                if (!receivedText)
                {
                    receivedText = true;
                    await MainThread.InvokeOnMainThreadAsync(() => reply.Progress = "");
                }

                // In a multi-agent Group Chat workflow, label each speaker as they take the floor.
                if (update.AuthorName is "PracticalBuilder" or "SystemArchitect" or "TrackModerator"
                    && update.AuthorName != activeDebateSpeaker)
                {
                    var prefix = activeDebateSpeaker is null
                        ? $"**{update.AuthorName}:** "
                        : $"\n\n**{update.AuthorName}:** ";
                    activeDebateSpeaker = update.AuthorName;
                    yield return prefix;
                }

                yield return update.Text;
            }
        }

        reply.ResponseMessages = updates.ToChatResponse().Messages.ToArray();
        await tools.AddReferencedSessionsAsync(reply.Text);

        // When the DayPlan workflow recommends a schedule, offer a one-click approval card to favourite all picks.
        if (isDayPlan && reply.Sessions.Count > 0 && reply.Approvals.Count == 0)
        {
            var approval = new ChatApproval(reply.Sessions.Select(card => card.Session).ToArray(), add: true);
            await MainThread.InvokeOnMainThreadAsync(() => reply.Approvals.Add(approval));

            if (await approval.WaitAsync(cancellationToken))
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    preferences.SetFavorites(reply.Sessions.Select(card => card.Session.Id), isFavorite: true));
                yield return $"\n\nAdded all {reply.Sessions.Count} recommended talks to favourites.";
            }
            else
            {
                yield return "\n\nPlan not saved. Your favourites haven't changed.";
            }
        }

        logger.LogInformation("[AI Chat] Completed reply ({Length} chars, {Cards} session cards).",
            reply.Text.Length, reply.Sessions.Count);
    }

    internal static bool IsDayPlanRequest(string request) =>
        request.Equals("Plan my day", StringComparison.OrdinalIgnoreCase) ||
        request.StartsWith("Plan my day:", StringComparison.OrdinalIgnoreCase) ||
        request.StartsWith("Plan my day ", StringComparison.OrdinalIgnoreCase);
}
