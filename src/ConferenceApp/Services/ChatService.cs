using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.AI;
using ConferenceApp.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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

// Manages conversation history, streams responses from the assistant IChatClient, and resolves referenced Session cards.
public class ChatService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    [FromKeyedServices("assistant")] IChatClient chatClient,
    ILogger<ChatService> logger)
{
    private readonly List<ChatMessage> history = [];

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
        history.Clear();
    }

    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        Sessions.Clear();
        Messages.Add(new ChatItem(isUser: true, text));

        var reply = new ChatItem(isUser: false)
        {
            Progress = "Thinking..."
        };
        Messages.Add(reply);

        List<ChatMessage> turnHistory =
        [
            .. history,
            new(ChatRole.User, text)
        ];

        var itemsByMessage = new Dictionary<string, ChatItem>();
        var lastItem = reply;
        var updates = new List<ChatResponseUpdate>();

        try
        {
            await foreach (var update in chatClient.GetStreamingResponseAsync(turnHistory, cancellationToken: cancellationToken))
            {
                updates.Add(update);

                foreach (var call in update.Contents.OfType<FunctionCallContent>())
                    AddToolCall(reply, call.Name);

                if (update.Role is null || update.Role == ChatRole.Assistant)
                    lastItem = AppendText(reply, itemsByMessage, update.Text, update.AuthorName, update.MessageId, update.ModelId);
            }

            history.Add(new(ChatRole.User, text));
            history.AddRange(updates
                .ToChatResponse()
                .Messages
                .Where(m => !string.IsNullOrWhiteSpace(m.Text) ||
                            m.Contents.Any(c => c is FunctionCallContent or FunctionResultContent)));

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

    // Each agent turn gets its own bubble (keyed by message), so a group chat agent speaking twice gets two bubbles.
    private ChatItem AppendText(ChatItem reply, Dictionary<string, ChatItem> itemsByMessage, string? text, string? author = null, string? messageId = null, string? modelId = null)
    {
        if (string.IsNullOrEmpty(text))
            return itemsByMessage.Values.LastOrDefault() ?? reply;

        var key = messageId ?? author ?? "";
        if (!itemsByMessage.TryGetValue(key, out var item))
        {
            item = itemsByMessage.Count == 0 ? reply : new ChatItem(isUser: false);
            // Show which model answered, e.g. "conference-assistant (apple-intelligence)".
            item.Author = string.IsNullOrEmpty(modelId) ? author : $"{author} ({modelId})";
            item.Progress = "";
            if (item != reply)
                Messages.Add(item);
            itemsByMessage[key] = item;
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
