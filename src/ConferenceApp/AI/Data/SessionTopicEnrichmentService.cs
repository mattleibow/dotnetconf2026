using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

// Background AI enrichment worker that populates Session.Topics using KeywordEnricher.
public sealed class SessionTopicEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    EnricherOptions enricherOptions,
    ILogger<SessionTopicEnrichmentService> logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
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

    protected override async Task ExecuteAsync()
    {
        if (await ChatExtensions.GetModelAvailabilityAsync() != ChatModelAvailability.Available)
            return;

        foreach (var session in await GetTalkSessionsAsync())
        {
            await TryExtractTopicsAsync(session, session.EmbeddingText);

            if (session.Topics.Count == 0 && !string.IsNullOrWhiteSpace(session.Summary))
                await TryExtractTopicsAsync(session, $"{session.Title} — {session.Summary}");

            if (session.Topics.Count == 0)
                await TryExtractTopicsAsync(session, session.Title);

            Logger.LogInformation("[AI] Topics for {SessionId}: [{Topics}]",
                session.Id, string.Join(", ", session.Topics));
        }
    }

    private async Task TryExtractTopicsAsync(Session session, string text)
    {
        var chunk = new IngestionChunk<string>(text, new IngestionDocument(session.Id));

        await foreach (var enriched in enricher.ProcessAsync(AsyncEnumerable.Repeat(chunk, 1)))
        {
            if (!enriched.Metadata.TryGetValue(KeywordEnricher.MetadataKey, out var value) ||
                value is not IEnumerable<string> keywords)
            {
                continue;
            }

            var matched = keywords
                .SelectMany(k => Topics.Where(t =>
                    k.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    (t == "AI" && (
                        k.Contains("neural", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("model", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("machine learning", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("deep learning", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("llm", StringComparison.OrdinalIgnoreCase))) ||
                    (t == "Data" && (
                        k.Contains("algorithm", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("optimiz", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("analytics", StringComparison.OrdinalIgnoreCase) ||
                        k.Contains("graph", StringComparison.OrdinalIgnoreCase)))))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();

            if (matched.Length > 0)
                session.Topics = matched;
        }
    }
}
