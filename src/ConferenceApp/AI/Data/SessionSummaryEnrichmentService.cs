using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

/// <summary>
/// Background AI enrichment worker that populates Session.Summary using SummaryEnricher.
/// </summary>
public sealed class SessionSummaryEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    EnricherOptions enricherOptions,
    ILogger<SessionSummaryEnrichmentService> logger)
{
    private readonly SummaryEnricher enricher = new(enricherOptions, maxWordCount: 24);
    private Task? processing;

    public Task StartAsync() => processing ??= Task.Run(EnrichAsync);

    private async Task EnrichAsync()
    {
        try
        {
            if (await ChatExtensions.GetModelAvailabilityAsync() != ChatModelAvailability.Available)
                return;

            var db = await dbProvider.GetDbAsync();
            var activeCity = await preferences.GetSelectedCityAsync();

            var sessions = db.Sessions
                .Where(s => s.IsTalk && !string.IsNullOrWhiteSpace(s.Description))
                .OrderByDescending(s => s.CityId == activeCity.Id)
                .ToList();

            foreach (var session in sessions)
            {
                logger.LogInformation("[AI] Summarizing session {SessionId}...", session.Id);
                await TrySummarizeAsync(session, session.EmbeddingText);

                if (string.IsNullOrWhiteSpace(session.Summary))
                    await TrySummarizeAsync(session, session.Title);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[AI] Background session summary enrichment stopped.");
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

    private static async IAsyncEnumerable<IngestionChunk<string>> SingleChunk(IngestionChunk<string> chunk)
    {
        yield return chunk;
        await Task.CompletedTask;
    }
}
