using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

internal static class EnrichmentRequest
{
    public static async Task<(T? Result, bool TimedOut)> GetAsync<T>(
        IChatClient chatClient, ILogger logger, string prompt, string text,
        string kind, string id) where T : class
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        Task? pending = null;

        try
        {
            var request = chatClient.GetResponseAsync<T>(
                new ChatMessage[] { new(ChatRole.System, prompt), new(ChatRole.User, text) },
                new ChatOptions { Temperature = 0, MaxOutputTokens = 512 },
                useJsonSchemaResponseFormat: true,
                cancellationToken: deadline.Token);
            pending = request;
            // Move on at the deadline even if Windows native work is still running; overlap is accepted.
            var response = await request.WaitAsync(deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();

            if (response.TryGetResult(out var result) && result is not null)
                return (result, false);

            logger.LogWarning("[AI] No valid structured output for {Kind} {ItemId}.", kind, id);
        }
        catch (Exception) when (deadline.IsCancellationRequested)
        {
            logger.LogWarning("[AI] Enrichment timed out for {Kind} {ItemId}; moving on without retrying.", kind, id);
            return (null, true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "[AI] Enrichment request failed for {Kind} {ItemId}.", kind, id);
        }
        finally
        {
            if (pending is not null && deadline.IsCancellationRequested)
                _ = pending.ContinueWith(task => logger.LogWarning(task.Exception,
                        "[AI] Cancelled {Kind} request for {ItemId} later failed.", kind, id),
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        return (null, false);
    }
}
