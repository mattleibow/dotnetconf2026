namespace ConferenceApp.Models;

public record ChatSessionCard(Session Session, string Metadata, string Speakers, string Reason = "");
