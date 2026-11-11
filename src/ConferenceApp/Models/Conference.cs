using CommunityToolkit.Mvvm.ComponentModel;

namespace ConferenceApp.Models;

public sealed class Conference
{
    public string Name { get; set; } = "";
    public List<City> Cities { get; set; } = [];
    public List<Session> Sessions { get; set; } = [];
    public List<Speaker> Speakers { get; set; } = [];
    public List<Venue> Venues { get; set; } = [];
    public List<Sponsor> Sponsors { get; set; } = [];
}

public sealed class City
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateOnly Date { get; set; }
    public string VenueId { get; set; } = "";
    public string RegistrationUrl { get; set; } = "";
}

public sealed partial class Session : ObservableObject
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
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasTopic))] public partial string Topic { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSummary))] public partial string Summary { get; set; } = "";
    [ObservableProperty] public partial bool SummaryUnavailable { get; set; }
    public bool HasTopic => !string.IsNullOrEmpty(Topic);
    public bool HasSummary => !string.IsNullOrEmpty(Summary);
}

public sealed class Speaker
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Tagline { get; set; }
    public List<string> CityIds { get; set; } = [];
    public string Content { get; set; } = "";
    public string? Image { get; set; }
}

public sealed class Venue
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string MapUrl { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Image { get; set; }
}

public sealed class Sponsor
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Tier { get; set; } = "";
    public List<string> CityIds { get; set; } = [];
    public string? Image { get; set; }
}
