using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.Data;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.Services;

// Represents a user or assistant turn in the Assistant conversation, with optional attached Session cards.
public partial class ChatItem(bool isUser, string text = "") : ObservableObject
{
    public bool IsUser { get; } = isUser;
    public ObservableCollection<Session> Sessions { get; } = [];

    [ObservableProperty]
    public partial string Text { get; set; } = text;

    [ObservableProperty]
    public partial string Progress { get; set; } = "";
}

// Streams responses from the local on-device IChatClient and attaches referenced Session cards.
public class ChatService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
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

    public Task<string?> GetUnavailableReasonAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>("On-device AI isn't connected yet. In Part 2 we'll wire up local embeddings, data enrichment, tools, and multi-agent workflows.");

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

        await AddReferencedSessionsAsync(reply);

        logger.LogInformation("[AI Chat] Completed reply ({Length} chars, {Cards} session cards).",
            reply.Text.Length, reply.Sessions.Count);
    }

    // Attaches tappable session cards under the assistant reply for any talk mentioned in the response.
    private async Task AddReferencedSessionsAsync(ChatItem reply)
    {
        var db = await dbProvider.GetDbAsync();
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
}
