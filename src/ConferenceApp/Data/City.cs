using System.Text.Json.Serialization;

namespace ConferenceApp.Data;

public record City(
    string Id,
    string Name,
    DateOnly Date,
    string VenueId)
{
    [JsonIgnore]
    public Venue Venue { get; set; } = null!;
}
