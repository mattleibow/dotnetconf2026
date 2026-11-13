using System.Text.Json.Serialization;
using Microsoft.Extensions.VectorData;

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
    string VenueId)
{
    [JsonIgnore]
    public Venue Venue { get; set; } = null!;
}

public class Session
{
    [VectorStoreKey]
    public string Id { get; set; } = "";

    [VectorStoreData]
    public string Title { get; set; } = "";

    [VectorStoreData(IsIndexed = true)]
    public string CityId { get; set; } = "";

    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? Track { get; set; }
    public List<string> SpeakerIds { get; set; } = [];
    public string Kind { get; set; } = "";
    public string Content { get; set; } = "";

    public string? Description { get; set; }
    public string? Summary { get; set; }
    public string? Topic { get; set; }

    [JsonIgnore]
    public City City { get; set; } = null!;

    [JsonIgnore]
    public IReadOnlyList<Speaker> Speakers { get; set; } = [];

    [JsonIgnore]
    public bool IsTalk => Kind == "session";

    [JsonIgnore]
    public string Time => $"{Start:HH:mm} – {End:HH:mm}";

    [JsonIgnore]
    public string Meta => Track is { Length: > 0 } ? $"{Time} · {Track}" : Time;

    [JsonIgnore]
    public string SpeakerNames => string.Join(", ", Speakers.Select(s => s.Name));

    [JsonIgnore]
    public string CityMeta => $"{City?.Name} · {Time} · {Track ?? "All attendees"}";

    [JsonIgnore]
    [VectorStoreVector(384, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string EmbeddingText => $"{Title}\n\n{Description}";
}

public record Speaker(
    string Id,
    string Name,
    string? Tagline,
    List<string> CityIds,
    string Content,
    string? Image)
{
    public string? Bio { get; set; }

    [JsonIgnore]
    public string Initials => string.Concat(
        Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => part[0]));
}

public record Venue(
    string Id,
    string Name,
    string CityId,
    string Address,
    double Latitude,
    double Longitude,
    string MapUrl,
    string Content,
    string? Image)
{
    public string? Description { get; set; }
}

public record Sponsor(
    string Id,
    string Name,
    string Tier,
    List<string> CityIds,
    string Url,
    string? Image);
