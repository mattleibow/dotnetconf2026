using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public abstract class EnrichmentService<TItem>(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IChatClient chatClient,
    ILogger logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    protected abstract string Instructions { get; }
    protected abstract Task<List<TItem>> GetItemsAsync();
    protected abstract Task<bool> EnrichAsync(TItem item);

    protected sealed override async Task ExecuteAsync()
    {
        if (await ChatExtensions.GetModelAvailabilityAsync(logger: Logger) != ChatModelAvailability.Available)
        {
            Logger.LogInformation("[AI] {Name} enrichment skipped: model unavailable.", typeof(TItem).Name);
            return;
        }

        var items = await GetItemsAsync();
        var complete = 0;
        var timedOut = 0;

        foreach (var item in items)
        {
            try
            {
                if (await EnrichAsync(item))
                    complete++;
            }
            catch (TimeoutException)
            {
                timedOut++;
            }
        }

        Logger.LogInformation("[AI] {Name} enrichment finished: {Complete} of {Total} complete, {TimedOut} timed out.",
            typeof(TItem).Name, complete, items.Count, timedOut);
    }

    protected async Task<T?> AskAsync<T>(string label, string prompt) where T : class
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        try
        {
            ChatMessage[] messages =
            [
                new(ChatRole.System, Instructions),
                new(ChatRole.User, prompt)
            ];

            var options = new ChatOptions { Temperature = 0, MaxOutputTokens = 512 };
            var request = chatClient.GetResponseAsync<T>(messages, options, cancellationToken: deadline.Token);

            // Native work can ignore cancellation; skip at the deadline so late output never reaches Apply.
            var response = await request.WaitAsync(deadline.Token);

            if (!response.TryGetResult(out var result) || result is null)
            {
                Logger.LogWarning("[AI] Enrichment for {Label} returned unusable structured output.", label);
                return null;
            }

            return result;
        }
        catch (Exception exception) when (deadline.IsCancellationRequested)
        {
            Logger.LogWarning(exception, "[AI] Enrichment for {Label} timed out; moving on without retrying.", label);
            throw new TimeoutException($"Enrichment for {label} timed out.", exception);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "[AI] Enrichment request for {Label} failed.", label);
            return null;
        }
    }
}
