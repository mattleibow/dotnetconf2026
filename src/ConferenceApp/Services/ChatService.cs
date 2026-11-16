using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.Data;

namespace ConferenceApp.Services;

public partial class ChatItem(bool isUser, string text = "") : ObservableObject
{
    public bool IsUser { get; } = isUser;
    public ObservableCollection<Session> Sessions { get; } = [];

    [ObservableProperty]
    public partial string Text { get; set; } = text;

    [ObservableProperty]
    public partial string Progress { get; set; } = "";
}

public class ChatService
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
        Task.FromResult<string?>(null);

    public async IAsyncEnumerable<string> SendAsync(
        IReadOnlyList<ChatItem> messages,
        ChatItem reply,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return "On-device AI isn't connected yet. In Part 2 we'll wire up local embeddings, data enrichment, tools, and multi-agent workflows.";
    }
}
