using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

/// <summary>
/// Background AI enrichment worker that populates Session.Topics using KeywordEnricher.
/// </summary>
public sealed class SessionTopicEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    EnricherOptions enricherOptions,
    ILogger<SessionTopicEnrichmentService> logger)
{
    public static readonly string[] Topics =
    [
        "AI",
        "Agents",
        "Cloud",
        "Data",
        "Mobile",
        "Web",
        "IoT",
        "Architecture",
        "Security",
        "DevOps",
        "Gaming"
    ];

    private readonly KeywordEnricher enricher = new(
        enricherOptions,
        predefinedKeywords: Topics,
        maxKeywords: 3,
        confidenceThreshold: 0.7);

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
                await TryExtractTopicsAsync(session, session.EmbeddingText);

                if (session.Topics.Count == 0)
                    await TryExtractTopicsAsync(session, session.Title);

                logger.LogInformation("[AI] Topics for {SessionId}: [{Topics}]",
                    session.Id, string.Join(", ", session.Topics));
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[AI] Background session topic enrichment stopped.");
        }
    }

    private async Task TryExtractTopicsAsync(Session session, string text)
    {
        var chunk = new IngestionChunk<string>(text, new IngestionDocument(session.Id));

        await foreach (var enriched in enricher.ProcessAsync(SingleChunk(chunk)))
        {
            if (!enriched.Metadata.TryGetValue(KeywordEnricher.MetadataKey, out var value) ||
                value is not IEnumerable<string> keywords)
            {
                continue;
            }

            var matched = keywords
                .Select(k => Topics.FirstOrDefault(t => k.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();

            if (matched.Length > 0)
                session.Topics = matched;
        }
    }

    private static async IAsyncEnumerable<IngestionChunk<string>> SingleChunk(IngestionChunk<string> chunk)
    {
        yield return chunk;
        await Task.CompletedTask;
    }
}
