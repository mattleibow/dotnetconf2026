using ConferenceApp.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace ConferenceApp.AI;

/// <summary>
/// Background vector indexing service: populates VectorStoreCollection&lt;string, Session&gt;
/// from the loaded ConferenceDb using the [VectorStoreVector] EmbeddingText property.
/// </summary>
public sealed class SessionIndexingService(
    ConferenceDbProvider dbProvider,
    VectorStoreCollection<string, Session> sessionVectors,
    ILogger<SessionIndexingService> logger)
{
    private Task? indexing;

    public Task StartAsync() => indexing ??= Task.Run(IndexAsync);

    private async Task IndexAsync()
    {
        try
        {
            var db = await dbProvider.GetDbAsync();
            var sessions = db.Sessions
                .Where(s => s.IsTalk && !string.IsNullOrWhiteSpace(s.Description))
                .ToList();

            await sessionVectors.EnsureCollectionExistsAsync();
            await sessionVectors.UpsertAsync(sessions);
            logger.LogInformation("[AI] Indexed {Count} session abstracts into VectorStoreCollection<string, Session>.", sessions.Count);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[AI] Background session vector indexing stopped.");
        }
    }
}
