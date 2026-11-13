namespace ConferenceApp.Data;

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
