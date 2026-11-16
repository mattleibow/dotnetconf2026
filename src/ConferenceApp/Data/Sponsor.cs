namespace ConferenceApp.Data;

public record Sponsor(
    string Id,
    string Name,
    string Tier,
    List<string> CityIds,
    string Url,
    string? Image);
