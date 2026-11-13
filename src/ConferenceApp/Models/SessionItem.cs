namespace ConferenceApp.Models;

public record SessionItem(
    Session Session,
    string Speakers,
    bool IsFavorite,
    string? CityName = null)
{
    public string Meta => (CityName, Session.Track) switch
    {
        ({ Length: > 0 } city, { Length: > 0 } track) => $"{city} · {Session.Time} · {track}",
        ({ Length: > 0 } city, _) => $"{city} · {Session.Time}",
        (_, { Length: > 0 } track) => $"{Session.Time} · {track}",
        _ => Session.Time
    };

    public string? Summary => Session.Summary;
    public string? Topic => Session.Topic;
}
