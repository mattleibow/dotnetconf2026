using ConferenceApp.Data;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.Services;

public abstract class BaseBackgroundService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    ILogger logger)
{
    private Task? processing;

    protected ConferenceDbProvider DbProvider { get; } = dbProvider;
    protected UserPreferencesService Preferences { get; } = preferences;
    protected ILogger Logger { get; } = logger;

    public Task StartAsync() => processing ??= Task.Run(RunCoreAsync);

    private async Task RunCoreAsync()
    {
        try
        {
            await ExecuteAsync();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Background service {ServiceName} stopped.", GetType().Name);
        }
    }

    protected async Task<List<Session>> GetTalkSessionsAsync()
    {
        var db = await DbProvider.GetDbAsync();
        var activeCity = await Preferences.GetSelectedCityAsync();

        return db.Sessions
            .Where(s => s.IsTalk && !string.IsNullOrWhiteSpace(s.Description))
            .OrderByDescending(s => s.CityId == activeCity.Id)
            .ToList();
    }

    protected static async IAsyncEnumerable<IngestionChunk<string>> SingleChunk(IngestionChunk<string> chunk)
    {
        yield return chunk;
        await Task.CompletedTask;
    }

    protected abstract Task ExecuteAsync();
}
