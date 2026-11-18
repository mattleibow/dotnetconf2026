using ConferenceApp.Data;
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

    protected abstract Task ExecuteAsync();
}
