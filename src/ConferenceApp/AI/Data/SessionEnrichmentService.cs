using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

/// <summary>
/// Background AI enrichment worker: populates in-memory Session.Summary and Session.Topic
/// using the DI-registered SummaryEnricher and KeywordEnricher (up to 3 topics >= 75% confidence).
/// Because Session.Summary and Session.Topic are [ObservableProperty]s, XAML bindings update
/// cards in place automatically without rebuilding the CollectionView.
/// </summary>
public sealed class SessionEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    SummaryEnricher summaryEnricher,
    KeywordEnricher keywordEnricher,
    ILogger<SessionEnrichmentService> logger)
{
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

            // Enrich the currently selected city's talks first so the visible Agenda populates immediately.
            var sessions = db.Sessions
                .Where(s => s.IsTalk && !string.IsNullOrWhiteSpace(s.Description))
                .OrderByDescending(s => s.CityId == activeCity.Id)
                .ToList();

            foreach (var session in sessions)
            {
                logger.LogInformation("[AI] Enriching session {SessionId} with SummaryEnricher + KeywordEnricher...", session.Id);
                var chunk = new IngestionChunk<string>(
                    session.EmbeddingText,
                    new IngestionDocument(session.Id));

                await foreach (var enriched in keywordEnricher.ProcessAsync(summaryEnricher.ProcessAsync(SingleChunk(chunk))))
                {
                    var summary = ReadSummary(enriched);
                    var topics = ReadTopics(enriched);

                    if (!string.IsNullOrWhiteSpace(summary))
                        session.Summary = summary;
                    if (!string.IsNullOrWhiteSpace(topics))
                        session.Topic = topics;
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[AI] Background session enrichment stopped.");
        }
    }

    private static string ReadSummary(IngestionChunk<string> chunk) =>
        chunk.Metadata.TryGetValue(SummaryEnricher.MetadataKey, out var value) && value is string text
            ? string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            : "";

    private static string ReadTopics(IngestionChunk<string> chunk)
    {
        if (!chunk.Metadata.TryGetValue(KeywordEnricher.MetadataKey, out var value) ||
            value is not IEnumerable<string> keywords)
        {
            return "";
        }

        var matched = keywords
            .Select(k => DataExtensions.Topics.FirstOrDefault(t => k.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Where(t => t is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3);

        return string.Join(" · ", matched);
    }

    private static async IAsyncEnumerable<IngestionChunk<string>> SingleChunk(IngestionChunk<string> chunk)
    {
        yield return chunk;
        await Task.CompletedTask;
    }
}
