using ConferenceApp.Services;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.Data;

public sealed class DatabasePreloadService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    ILogger<DatabasePreloadService> logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    protected override Task ExecuteAsync() => DbProvider.GetDbAsync();
}
