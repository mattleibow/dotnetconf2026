using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.Data;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.Services;

// Represents a user, tool call, or assistant item in the Assistant conversation.
public partial class ChatItem(bool isUser, string text = "", string? author = null, bool isTool = false) : ObservableObject
{
    public bool IsUser { get; } = isUser;
    public bool IsTool { get; } = isTool;

    [ObservableProperty]
    public partial string? Author { get; set; } = author;

    [ObservableProperty]
    public partial string Text { get; set; } = text;

    [ObservableProperty]
    public partial string Progress { get; set; } = "";
}

// Manages conversation history, streams responses from the local on-device IChatClient, and resolves referenced Session cards.
public class ChatService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    ILogger<ChatService> logger)
{
    public ObservableCollection<ChatItem> Messages { get; } = [];
    public ObservableCollection<Session> Sessions { get; } = [];

    public IReadOnlyList<string> Suggestions { get; } =
    [
        "When is Matthew's talk?",
        "Prepare me for Matthew's talk: takeaways and questions",
        "Can you suggest some talks for me?",
        "Do my favourites clash?"
    ];

    public void Clear()
    {
        // Clear the footer cards first: Android's CollectionView measures the footer when it
        // swaps to the EmptyView, and a stale tall footer would clip the welcome screen.
        Sessions.Clear();
        Messages.Clear();
    }

    public Task<string?> GetUnavailableReasonAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>("On-device AI isn't connected yet. In Part 2 we'll wire up local embeddings, data enrichment, tools, and multi-agent workflows.");

    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        Sessions.Clear();
        Messages.Add(new ChatItem(isUser: true, text));

        var reply = new ChatItem(isUser: false)
        {
            Progress = "Thinking on device..."
        };
        Messages.Add(reply);

        if (await GetUnavailableReasonAsync(cancellationToken) is { } reason)
        {
            reply.Progress = "";
            reply.Text = reason;
            return;
        }

        var itemsByAuthor = new Dictionary<string, ChatItem>();
        var lastItem = reply;

        try
        {
            await UpdateReferencedSessionsAsync(lastItem.Text);

            logger.LogInformation("[AI Chat] Completed reply ({Length} chars, {Cards} session cards).",
                lastItem.Text.Length, Sessions.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            lastItem.Progress = "";
            lastItem.Text = string.IsNullOrWhiteSpace(lastItem.Text) ? ex.Message : $"{lastItem.Text}\n\n{ex.Message}";
        }
        finally
        {
            reply.Progress = "";
            lastItem.Progress = "";
        }
    }

    private void AddToolCall(ChatItem reply, string? toolName)
    {
        if (!string.IsNullOrWhiteSpace(toolName))
            Messages.Insert(Messages.IndexOf(reply), new ChatItem(isUser: false, toolName, isTool: true));
    }

    private ChatItem AppendText(ChatItem reply, Dictionary<string, ChatItem> itemsByAuthor, string? text, string? author = null)
    {
        if (string.IsNullOrEmpty(text))
            return itemsByAuthor.Values.LastOrDefault() ?? reply;

        var key = author ?? "";
        if (!itemsByAuthor.TryGetValue(key, out var item))
        {
            item = itemsByAuthor.Count == 0 ? reply : new ChatItem(isUser: false);
            item.Author = author;
            item.Progress = "";
            if (item != reply)
                Messages.Add(item);
            itemsByAuthor[key] = item;
        }

        item.Text += text;
        return item;
    }

    // Populates tappable session cards in the footer for any talk mentioned in the final response.
    private async Task UpdateReferencedSessionsAsync(string responseText)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();

        var referenced = db.Sessions
            .Where(s => s.CityId == city.Id && s.IsTalk && (
                responseText.Contains(s.Title, StringComparison.OrdinalIgnoreCase) ||
                responseText.Contains(s.Id, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        foreach (var session in referenced)
        {
            if (Sessions.Any(s => s.Id == session.Id))
                continue;

            Sessions.Add(session);
        }
    }
}
