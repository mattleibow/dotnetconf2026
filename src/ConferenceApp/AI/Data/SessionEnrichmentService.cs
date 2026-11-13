using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Data;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public record SessionsEnrichedMessage;

/// <summary>
/// Background AI enrichment worker: populates in-memory Session.Summary and Session.Topic
/// using the DI-registered SummaryEnricher and ClassificationEnricher.
/// </summary>
public sealed class SessionEnrichmentService(
    ConferenceDbProvider dbProvider,
    ILogger<SessionEnrichmentService> logger,
    SummaryEnricher? summaryEnricher = null,
    ClassificationEnricher? classifier = null)
{
    private Task? processing;

    public Task StartAsync() => processing ??= Task.Run(EnrichAsync);

    private async Task EnrichAsync()
    {
        try
        {
            if (summaryEnricher is null || classifier is null ||
                await ChatExtensions.GetModelAvailabilityAsync() != ChatModelAvailability.Available)
                return;

            var db = await dbProvider.GetDbAsync();
            var sessions = db.Sessions
                .Where(s => s.IsTalk && !string.IsNullOrWhiteSpace(s.Description))
                .ToList();

            foreach (var session in sessions)
            {
                logger.LogInformation("[AI] Enriching session {SessionId} with SummaryEnricher + ClassificationEnricher...", session.Id);
                var chunk = new IngestionChunk<string>(
                    session.EmbeddingText,
                    new IngestionDocument(session.Id));

                await foreach (var enriched in classifier.ProcessAsync(summaryEnricher.ProcessAsync(SingleChunk(chunk))))
                {
                    var summary = ReadMetadata(enriched, SummaryEnricher.MetadataKey);
                    var topic = ReadMetadata(enriched, ClassificationEnricher.MetadataKey);

                    session.Summary = summary;
                    session.Topic = DataExtensions.Topics.Contains(topic, StringComparer.Ordinal) ? topic : null;
                }

                WeakReferenceMessenger.Default.Send(new SessionsEnrichedMessage());
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[AI] Background session enrichment stopped.");
        }
    }

    private static string ReadMetadata(IngestionChunk<string> chunk, string key) =>
        chunk.Metadata.TryGetValue(key, out var value) && value is string text
            ? string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            : "";

    private static async IAsyncEnumerable<IngestionChunk<string>> SingleChunk(IngestionChunk<string> chunk)
    {
        yield return chunk;
        await Task.CompletedTask;
    }
}
