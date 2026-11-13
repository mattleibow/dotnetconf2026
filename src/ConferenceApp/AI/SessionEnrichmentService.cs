using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Data;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public record SessionsEnrichedMessage;

/// <summary>
/// Background AI enrichment worker: populates in-memory Session.Summary and Session.Topic
/// using Microsoft.Extensions.DataIngestion enrichers and the local IChatClient.
/// </summary>
public sealed class SessionEnrichmentService(
    ConferenceDbProvider dbProvider,
    ILoggerFactory loggerFactory,
    ILogger<SessionEnrichmentService> logger,
    IChatClient? client = null)
{
    private static readonly string[] Topics = ["AI", "Mobile", "Web", "Cloud", "Data", "IoT"];
    private Task? processing;

    public Task StartAsync() => processing ??= Task.Run(EnrichAsync);

    private async Task EnrichAsync()
    {
        try
        {
            if (client is null || await ChatExtensions.GetModelAvailabilityAsync() != ChatModelAvailability.Available)
                return;

            var db = await dbProvider.GetDatabaseAsync();
            var sessions = db.Sessions
                .Where(s => s.IsTalk && !string.IsNullOrWhiteSpace(s.Description))
                .ToList();

            var enricherOptions = new EnricherOptions(client)
            {
                BatchSize = 1,
                ChatOptions = new ChatOptions { Temperature = 0, MaxOutputTokens = 160 },
                LoggerFactory = loggerFactory
            };

            var summaryEnricher = new SummaryEnricher(enricherOptions, maxWordCount: 24);
            var classifier = new ClassificationEnricher(enricherOptions, Topics, fallbackClass: "None");

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
                    session.Topic = Topics.Contains(topic, StringComparer.Ordinal) ? topic : null;
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
