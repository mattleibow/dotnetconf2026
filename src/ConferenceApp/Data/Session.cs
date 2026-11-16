using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConferenceApp.Data;

public partial class Session : ObservableObject
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public string CityId { get; set; } = "";

    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? Track { get; set; }
    public List<string> SpeakerIds { get; set; } = [];
    public string Kind { get; set; } = "";
    public string Content { get; set; } = "";

    public string? Description { get; set; }

    [ObservableProperty]
    public partial string? Summary { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Topics { get; set; } = [];

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
    public string EmbeddingText => $"{Title}\n\n{Description}";
}
