using System.Runtime.CompilerServices;
using ConferenceApp.Models;

namespace ConferenceApp.Services;

public class ChatService
{
    public IReadOnlyList<string> Suggestions { get; } =
    [
        "When is Matthew's talk?",
        "What's on after lunch?",
        "Do my favourites clash?"
    ];

    public async IAsyncEnumerable<string> SendAsync(
        IReadOnlyList<ChatItem> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return "The on-device AI assistant is not connected yet.";
    }
}
