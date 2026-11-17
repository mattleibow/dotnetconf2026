using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

// Background AI enrichment worker that populates Session.Summary using SummaryEnricher.
public sealed class SessionSummaryEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    EnricherOptions enricherOptions,
    ILogger<SessionSummaryEnrichmentService> logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    private readonly SummaryEnricher enricher = new(enricherOptions, maxWordCount: 24);

    protected override async Task ExecuteAsync()
    {
        if (await ChatExtensions.GetModelAvailabilityAsync() != ChatModelAvailability.Available)
            return;

        foreach (var session in await GetTalkSessionsAsync())
        {
            Logger.LogInformation("[AI] Summarizing session {SessionId}...", session.Id);
            await TrySummarizeAsync(session, session.EmbeddingText);

            if (string.IsNullOrWhiteSpace(session.Summary))
                await TrySummarizeAsync(session, session.Title);
        }
    }

    private async Task TrySummarizeAsync(Session session, string text)
    {
        var chunk = new IngestionChunk<string>(text, new IngestionDocument(session.Id));

        await foreach (var enriched in enricher.ProcessAsync(SingleChunk(chunk)))
        {
            if (enriched.Metadata.TryGetValue(SummaryEnricher.MetadataKey, out var value) &&
                value is string raw &&
                !string.IsNullOrWhiteSpace(raw))
            {
                session.Summary = string.Join(" ", raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            }
        }
    }
}
