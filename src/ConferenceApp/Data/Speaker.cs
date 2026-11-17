using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConferenceApp.Data;

public partial class Speaker : ObservableObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Tagline { get; set; }
    public List<string> CityIds { get; set; } = [];
    public string Content { get; set; } = "";
    public string? Image { get; set; }
    public string? Bio { get; set; }

    [ObservableProperty]
    [property: JsonIgnore]
    public partial string? Summary { get; set; }

    [ObservableProperty]
    [property: JsonIgnore]
    public partial IReadOnlyList<string> Expertise { get; set; } = [];

    [ObservableProperty]
    [property: JsonIgnore]
    public partial string? AskMeAbout { get; set; }

    [JsonIgnore]
    public string Initials => string.Concat(
        Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => part[0]));
}
