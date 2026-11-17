using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.AI;

// Background vector indexing service that populates VectorStoreCollection<string, Session>
// from the loaded ConferenceDb using the declarative [VectorStoreVector] EmbeddingText property.
public sealed class SessionIndexingService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    VectorStoreCollection<string, Session> sessionVectors,
    ILogger<SessionIndexingService> logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    protected override async Task ExecuteAsync()
    {
        var sessions = await GetTalkSessionsAsync();
        await sessionVectors.EnsureCollectionExistsAsync();
        await sessionVectors.UpsertAsync(sessions);
        Logger.LogInformation("[AI] Indexed {Count} session abstracts into VectorStoreCollection<string, Session>.", sessions.Count);
    }
}
