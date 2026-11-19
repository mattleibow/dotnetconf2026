using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.AI;
using ConferenceApp.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

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
    IChatClient chatClient,
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
        Messages.Clear();
        Sessions.Clear();
        history.Clear();
    }

    public async Task<string?> GetUnavailableReasonAsync(CancellationToken cancellationToken = default) =>
        await ChatExtensions.GetModelAvailabilityAsync(cancellationToken, logger) switch
        {
            ChatModelAvailability.Available => null,
            ChatModelAvailability.RequirementsNotMet =>
                "This device doesn't currently meet the requirements for on-device chat. You can still browse the agenda and favourites. No cloud service is used.",
            ChatModelAvailability.Disabled => "On-device AI is turned off. Enable it in your device settings to chat locally. No cloud service is used.",
            ChatModelAvailability.NotReady or ChatModelAvailability.Downloading =>
                "The on-device model isn't ready yet. Try again when it's ready.",
            _ => "On-device chat isn't available on this device right now. You can still browse the agenda and favourites. No cloud service is used."
        };

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

        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var favorites = db.Sessions
            .Where(s => s.CityId == city.Id && preferences.IsFavorite(s.Id))
            .Select(s => $"{s.Time} {s.Title}")
            .ToArray();
        var schedule = db.Sessions
            .Where(s => s.CityId == city.Id)
            .Select(s => $"- {s.Time} | {s.Title} ({s.SpeakerNames})");

        // Build conversation history with a basic inline system prompt for city, favourites, and schedule.
        List<ChatMessage> turnHistory =
        [
            new(ChatRole.System, $"""
                You are the .NET Conf South Africa assistant.
                Answer briefly using the schedule below.
                Current City: {city.Name} | Event Date: {city.Date:dddd, yyyy-MM-dd} | Current Time: 08:30
                Saved Favourites: {(favorites.Length > 0 ? string.Join("; ", favorites) : "None yet")}

                Schedule:
                {string.Join('\n', schedule)}
                """),
            .. history,
            new(ChatRole.User, text)
        ];

        var options = new ChatOptions
        {
            Temperature = 0
        };

        var itemsByAuthor = new Dictionary<string, ChatItem>();
        var lastItem = reply;
        var updates = new List<ChatResponseUpdate>();

        try
        {
            await foreach (var update in chatClient.GetStreamingResponseAsync(turnHistory, options, cancellationToken))
            {
                updates.Add(update);

                if (update.Role is null || update.Role == ChatRole.Assistant)
                    lastItem = AppendText(reply, itemsByAuthor, update.Text, update.AuthorName);
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
