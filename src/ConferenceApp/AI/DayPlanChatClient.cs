using System.Runtime.CompilerServices;
using ConferenceApp.Models;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public sealed class DayPlanChatClient(
    DayPlanService planner,
    ConferenceDatabase database,
    UserPreferencesService preferences,
    ConferenceAssistantTools tools,
    ILogger<DayPlanChatClient> logger) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
            updates.Add(update);
        return updates.ToChatResponse();
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var city = tools.CurrentCity;
        var reply = tools.CurrentReply;
        var selectedCityId = preferences.SelectedCityId;
        var request = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "Plan my day";

        logger.LogInformation("[AI Router] SemanticRoutingChatClient routed request to DayPlanWorkflow ('{Request}').", request);

        var plan = await planner.CreatePlanAsync(city, request,
            message => MainThread.InvokeOnMainThreadAsync(() => reply.Progress = message), cancellationToken);
        await MainThread.InvokeOnMainThreadAsync(() => reply.Progress = "");

        if (plan.Sessions.Count == 0)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant,
                $"I couldn't find talks in {city.Name} matching those interests and times. " +
                "No favourites changed. Try a broader interest or a later departure time.");
            yield break;
        }

        var conference = await database.GetConferenceAsync();
        var speakers = conference.Speakers.ToDictionary(s => s.Id, s => s.Name);
        var cards = plan.Sessions.Select(pick => new ChatSessionCard(pick.Session,
            $"{city.Name} · {pick.Session.Time} · {pick.Session.Track ?? "All attendees"}",
            string.Join(", ", pick.Session.SpeakerIds.Select(id => speakers[id])), pick.Reason)).ToArray();
        var approval = new ChatApproval(plan.Sessions.Select(pick => pick.Session).ToArray(), add: true);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CheckCurrentCity();
            foreach (var card in cards)
                reply.Sessions.Add(card);
            reply.Approvals.Add(approval);
        });

        var heading = $"Your {city.Name} plan for {city.Date:dddd, dd MMM}: {cards.Length} talks, " +
            $"{cards[0].Session.Start:HH:mm}–{cards[^1].Session.End:HH:mm}, with no overlaps. " +
            "Review the recommendations, then approve below to add the whole plan to favourites.";
        yield return new ChatResponseUpdate(ChatRole.Assistant, heading);

        var approved = await approval.WaitAsync(cancellationToken);
        if (approved)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CheckCurrentCity();
                preferences.SetFavorites(plan.Sessions.Select(pick => pick.Session.Id), isFavorite: true);
            });
        }

        var outcome = approved
            ? $"\n\nAdded all {cards.Length} recommended talks to favourites."
            : "\n\nPlan not saved. Your favourites haven't changed.";
        yield return new ChatResponseUpdate(ChatRole.Assistant, outcome);

        void CheckCurrentCity()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (preferences.SelectedCityId != selectedCityId)
                throw new InvalidOperationException("The city changed. Plan again using the current city.");
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
