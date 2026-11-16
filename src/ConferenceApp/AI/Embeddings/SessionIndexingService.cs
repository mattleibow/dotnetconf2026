using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.AI;

/// <summary>
/// Background vector indexing service: populates VectorStoreCollection&lt;string, Session&gt;
/// from the loaded ConferenceDb using the [VectorStoreVector] EmbeddingText property.
/// </summary>
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
