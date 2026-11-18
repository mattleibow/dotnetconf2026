using System.ComponentModel;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.AI;

public sealed class SpeakerEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IChatClient chatClient,
    ILogger<SpeakerEnrichmentService> logger)
    : EnrichmentService(dbProvider, preferences, chatClient, logger)
{
    protected override string Instructions =>
        """
        You are generating an attendee snapshot for a conference speaker badge.
        Using ONLY the speaker's biography and talk descriptions provided, produce a structured SpeakerProfile:
        - Summary: 1 concise sentence (max 22 words) capturing who they are and what they focus on.
        - Expertise: 2 to 3 short technical tags (1-2 words each).
        - AskMeAbout: 1 short, specific topic or question attendees can ask them about in the hallway (max 15 words).
        """;

    protected override async Task<int> EnrichAsync()
    {
        var enriched = 0;
        var db = await DbProvider.GetDbAsync();
        var activeCity = await Preferences.GetSelectedCityAsync();
        foreach (var speaker in db.Speakers.OrderByDescending(s => s.CityIds.Contains(activeCity.Id)))
        {
            var talks = db.Sessions
                .Where(s => s.IsTalk && s.SpeakerIds.Contains(speaker.Id))
                .Select(s => $"- {s.Title}: {s.Description}")
                .Distinct();
            var (profile, _) = await GetResponseAsync<SpeakerProfile>($"""
                Speaker: {speaker.Name}
                Tagline: {speaker.Tagline}
                Biography: {speaker.Bio}
                Talks:
                {string.Join("\n", talks)}
                """);
            if (profile is null)
                continue;

            Apply(speaker, profile);
            if (!string.IsNullOrWhiteSpace(speaker.Summary))
                enriched++;
            if (string.IsNullOrWhiteSpace(speaker.Summary) || speaker.Expertise.Count == 0 ||
                string.IsNullOrWhiteSpace(speaker.AskMeAbout))
                Logger.LogWarning("[AI] Speaker {SpeakerId} has incomplete profile fields after enrichment.", speaker.Id);
        }

        return enriched;
    }

    private static void Apply(Speaker speaker, SpeakerProfile profile)
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
