using System.ComponentModel;
using System.Runtime.CompilerServices;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

// Structured output schema for speaker profile enrichment (flat non-nullable fields for on-device models).
public sealed class SpeakerProfile
{
    [Description("A concise 1-sentence executive summary of the speaker's background and focus (max 22 words).")]
    public string Summary { get; set; } = "";

    [Description("Up to 3 short technical expertise tags (1-2 words each, e.g., '.NET MAUI', 'On-Device AI', 'Cloud Native').")]
    public string[] Expertise { get; set; } = [];

    [Description("A short, friendly hallway conversation starter or topic attendees can ask this speaker about (max 15 words).")]
    public string AskMeAbout { get; set; } = "";
}

// Custom DataIngestion chunk processor that extracts a structured SpeakerProfile using IChatClient.GetResponseAsync<SpeakerProfile>.
public sealed class SpeakerProfileEnricher(EnricherOptions options) : IngestionChunkProcessor<string>
{
    public const string MetadataKey = "speaker_profile";

    private const string SystemPrompt =
        """
        You are generating an attendee snapshot for a conference speaker badge.
        Using ONLY the speaker's biography and talk descriptions provided, produce a structured SpeakerProfile:
        - Summary: 1 concise sentence (max 22 words) capturing who they are and what they focus on.
        - Expertise: 2 to 3 short technical tags (1-2 words each).
        - AskMeAbout: 1 short, specific topic or question attendees can ask them about in the hallway (max 15 words).
        """;

    public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
        IAsyncEnumerable<IngestionChunk<string>> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in chunks.WithCancellation(cancellationToken))
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, chunk.Content)
            };

            var response = await options.ChatClient.GetResponseAsync<SpeakerProfile>(
                messages,
                options.ChatOptions,
                useJsonSchemaResponseFormat: true,
                cancellationToken: cancellationToken);

            if (response.TryGetResult(out var profile) && profile is not null)
                chunk.Metadata[MetadataKey] = profile;

            yield return chunk;
        }
    }
}

// Background AI enrichment worker that populates Speaker.Summary, Speaker.Expertise, and Speaker.AskMeAbout.
public sealed class SpeakerProfileEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    EnricherOptions enricherOptions,
    ILogger<SpeakerProfileEnrichmentService> logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    private readonly SpeakerProfileEnricher enricher = new(enricherOptions);

    protected override async Task ExecuteAsync()
    {
        if (await ChatExtensions.GetModelAvailabilityAsync() != ChatModelAvailability.Available)
            return;

        var db = await DbProvider.GetDbAsync();
        var activeCity = await Preferences.GetSelectedCityAsync();

        var speakers = db.Speakers
            .OrderByDescending(s => s.CityIds.Contains(activeCity.Id))
            .ToList();

        foreach (var speaker in speakers)
        {
            var talks = db.Sessions
                .Where(s => s.IsTalk && s.SpeakerIds.Contains(speaker.Id))
                .Select(s => $"- {s.Title}: {s.Description}")
                .Distinct();

            var text = $"""
                Speaker: {speaker.Name}
                Tagline: {speaker.Tagline}
                Biography: {speaker.Bio}
                Talks:
                {string.Join("\n", talks)}
                """;

            Logger.LogInformation("[AI] Enriching speaker profile for {SpeakerId}...", speaker.Id);
            await TryEnrichSpeakerAsync(speaker, text);
        }
    }

    private async Task TryEnrichSpeakerAsync(Speaker speaker, string text)
    {
        try
        {
            var chunk = new IngestionChunk<string>(text, new IngestionDocument(speaker.Id));

            await foreach (var enriched in enricher.ProcessAsync(SingleChunk(chunk)))
            {
                if (enriched.Metadata.TryGetValue(SpeakerProfileEnricher.MetadataKey, out var value) &&
                    value is SpeakerProfile profile)
                {
                    if (!string.IsNullOrWhiteSpace(profile.Summary))
                        speaker.Summary = profile.Summary.Trim();

                    var expertise = profile.Expertise
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .Select(e => e.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(3)
                        .ToArray();

                    if (expertise.Length > 0)
                        speaker.Expertise = expertise;

                    if (!string.IsNullOrWhiteSpace(profile.AskMeAbout))
                        speaker.AskMeAbout = profile.AskMeAbout.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[AI] Speaker profile enrichment skipped for {SpeakerId}.", speaker.Id);
        }
    }
}
