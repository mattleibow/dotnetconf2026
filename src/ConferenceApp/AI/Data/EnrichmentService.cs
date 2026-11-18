using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public abstract class EnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IChatClient chatClient,
    ILogger logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    protected abstract string Instructions { get; }
    protected abstract Task<int> EnrichAsync();

    protected sealed override async Task ExecuteAsync()
    {
        if (await ChatExtensions.GetModelAvailabilityAsync(logger: Logger) != ChatModelAvailability.Available)
            return;

        var enriched = await EnrichAsync();
        Logger.LogInformation("[AI] Enrichment completed. Items: {ItemCount}.", enriched);
    }

    protected async Task<(T? Result, bool TimedOut)> GetResponseAsync<T>(string input) where T : class
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        Task? pending = null;
        try
        {
            var request = chatClient.GetResponseAsync<T>(
                new ChatMessage[] { new(ChatRole.System, Instructions), new(ChatRole.User, input) },
                new ChatOptions { Temperature = 0, MaxOutputTokens = 512 },
                useJsonSchemaResponseFormat: true,
                cancellationToken: deadline.Token);
            pending = request;
            // Skip at the deadline without waiting for native work to drain; overlap is accepted.
            var response = await request.WaitAsync(deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            if (!response.TryGetResult(out var result) || result is null)
                throw new InvalidOperationException("The enrichment response did not contain valid structured output.");

            return (result, false);
        }
        catch (Exception exception) when (deadline.IsCancellationRequested)
        {
            Logger.LogWarning(exception, "[AI] Enrichment timed out; moving on without retrying.");
            return (null, true);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "[AI] Enrichment request failed.");
            return (null, false);
        }
        finally
        {
            if (pending is not null && deadline.IsCancellationRequested)
                _ = pending.ContinueWith(task => Logger.LogWarning(task.Exception,
                        "[AI] Timed-out enrichment request later failed."),
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }
}
