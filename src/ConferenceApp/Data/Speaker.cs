using System.Text.Json.Serialization;

namespace ConferenceApp.Data;

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
