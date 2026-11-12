using CommunityToolkit.Mvvm.ComponentModel;

namespace ConferenceApp.Models;

public partial class SessionItem(
    Session session,
    string speakers,
    bool isFavorite,
    string? summary = null,
    string? topic = null,
    string? cityName = null) : ObservableObject
{
    public Session Session { get; } = session;
    public string Speakers { get; } = speakers;
    public bool IsFavorite { get; } = isFavorite;

    public string Meta => (cityName, Session.Track) switch
    {
        ({ Length: > 0 } city, { Length: > 0 } track) => $"{city} · {Session.Time} · {track}",
        ({ Length: > 0 } city, _) => $"{city} · {Session.Time}",
        (_, { Length: > 0 } track) => $"{Session.Time} · {track}",
        _ => Session.Time
    };

    [ObservableProperty]
    public partial string? Summary { get; set; } = summary;

    [ObservableProperty]
    public partial string? Topic { get; set; } = topic;
}
