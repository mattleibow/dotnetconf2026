using System.ComponentModel;

namespace ConferenceApp.AI;

public sealed class SpeakerProfile
{
    [Description("A concise 1-sentence executive summary of the speaker's background and focus (max 22 words).")]
    public string Summary { get; set; } = "";

    [Description("Up to 3 short technical expertise tags (1-2 words each, e.g., '.NET MAUI', 'On-Device AI', 'Cloud Native').")]
    public string[] Expertise { get; set; } = [];

    [Description("A short, friendly hallway conversation starter or topic attendees can ask this speaker about (max 15 words).")]
    public string AskMeAbout { get; set; } = "";
}
