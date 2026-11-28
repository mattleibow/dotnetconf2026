using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

// After the gateway fails, fail fast for a short cooldown so OrderedFailoverChatClient goes straight to on-device AI.
// The cooldown is shared by every agent's client, so workflows don't each wait for the gateway timeout again.
public sealed class GatewayCooldownChatClient(IChatClient innerClient, ILogger logger) : DelegatingChatClient(innerClient)
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);
    private static long retryAfter;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfCoolingDown();
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            StartCooldown(ex);
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfCoolingDown();
        await using var updates = base.GetStreamingResponseAsync(messages, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            try
            {
                if (!await updates.MoveNextAsync())
                    yield break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                StartCooldown(ex);
                throw;
            }

            yield return updates.Current;
        }
    }

    private void ThrowIfCoolingDown()
    {
        var remaining = Interlocked.Read(ref retryAfter) - Environment.TickCount64;
        if (remaining <= 0)
            return;

        logger.LogInformation("[AI Chat] Gateway failed recently; using on-device AI for another {Seconds:0}s.", remaining / 1000.0);
        throw new InvalidOperationException("The AI gateway is cooling down after a failure.");
    }

    private void StartCooldown(Exception ex)
    {
        Interlocked.Exchange(ref retryAfter, Environment.TickCount64 + (long)Cooldown.TotalMilliseconds);
        logger.LogWarning("[AI Chat] Gateway failed ({Error}); using on-device AI for {Seconds}s.", ex.GetType().Name, Cooldown.TotalSeconds);
    }
}
