using System.ComponentModel;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public sealed class SpeakerEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IChatClient chatClient,
    ILogger<SpeakerEnrichmentService> logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    private const string SpeakerPrompt =
        """
        You are generating an attendee snapshot for a conference speaker badge.
        Using ONLY the speaker's biography and talk descriptions provided, produce a structured SpeakerProfile:
        - Summary: 1 concise sentence (max 22 words) capturing who they are and what they focus on.
        - Expertise: 2 to 3 short technical tags (1-2 words each).
        - AskMeAbout: 1 short, specific topic or question attendees can ask them about in the hallway (max 15 words).
        """;

    protected override async Task ExecuteAsync()
    {
        if (await ChatExtensions.GetModelAvailabilityAsync(logger: Logger) != ChatModelAvailability.Available)
            return;

        var enriched = 0;
        var db = await DbProvider.GetDbAsync();
        var activeCity = await Preferences.GetSelectedCityAsync();
        foreach (var speaker in db.Speakers.OrderByDescending(s => s.CityIds.Contains(activeCity.Id)))
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

            var (profile, _) = await EnrichmentRequest.GetAsync<SpeakerProfile>(
                chatClient, Logger, SpeakerPrompt, text, "speaker", speaker.Id);
            if (profile is null)
                continue;

            await MainThread.InvokeOnMainThreadAsync(() => ApplySpeakerProfile(speaker, profile));
            if (!string.IsNullOrWhiteSpace(speaker.Summary))
                enriched++;
            if (string.IsNullOrWhiteSpace(speaker.Summary) || speaker.Expertise.Count == 0 ||
                string.IsNullOrWhiteSpace(speaker.AskMeAbout))
                Logger.LogWarning("[AI] Speaker {SpeakerId} has incomplete profile fields after enrichment.", speaker.Id);
        }

        Logger.LogInformation("[AI] Speaker enrichment completed. Speakers: {SpeakerCount}.", enriched);
    }

    private static void ApplySpeakerProfile(Speaker speaker, SpeakerProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Summary))
            speaker.Summary = profile.Summary.Trim();

        var expertise = (profile.Expertise ?? [])
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

    public sealed class SpeakerProfile
    {
        [Description("A concise 1-sentence executive summary of the speaker's background and focus (max 22 words).")]
        public string Summary { get; set; } = "";

        [Description("Up to 3 short technical expertise tags (1-2 words each, e.g., '.NET MAUI', 'On-Device AI', 'Cloud Native').")]
        public string[] Expertise { get; set; } = [];

        [Description("A short, friendly hallway conversation starter or topic attendees can ask this speaker about (max 15 words).")]
        public string AskMeAbout { get; set; } = "";
    }
}
