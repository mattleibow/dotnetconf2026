using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace ConferenceApp.AI;

// Fallback no-op IChatClient used when the device or OS does not support local on-device AI.
public sealed class UnavailableChatClient : IChatClient
{
    private const string Message = "On-device chat isn't available on this device right now. You can still browse the agenda and favourites.";

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Message)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ChatResponseUpdate(ChatRole.Assistant, Message);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
