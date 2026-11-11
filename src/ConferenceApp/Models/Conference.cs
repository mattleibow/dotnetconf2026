namespace ConferenceApp.Models;

public record Conference(
    string Name,
    string TimeZone,
    List<City> Cities,
    List<Session> Sessions,
    List<Speaker> Speakers,
    List<Venue> Venues,
    List<Sponsor> Sponsors);

public record City(
    string Id,
    string Name,
    DateOnly Date,
    string VenueId);

public record Session(
    string Id,
    string Title,
    string CityId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Track,
    List<string> SpeakerIds,
    string Kind,
    string Content)
{
    public bool IsTalk => Kind == "session";
    public string Time => $"{Start:HH:mm} – {End:HH:mm}";
}

public record Speaker(
    string Id,
    string Name,
    string? Tagline,
    List<string> CityIds,
    string Content,
    string? Image);

public record Venue(
    string Id,
    string Name,
    string CityId,
    string Address,
    double Latitude,
    double Longitude,
    string MapUrl,
    string Content,
    string? Image);

public record Sponsor(
    string Id,
    string Name,
    string Tier,
    List<string> CityIds,
    string Url,
    string? Image);
