namespace ConferenceApp.Models;

public record ChatSessionCard(Session Session, string Metadata, string Speakers, string Reason = "")
{
    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);
}
