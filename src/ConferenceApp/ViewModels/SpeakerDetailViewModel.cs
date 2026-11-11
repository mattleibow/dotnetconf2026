using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SpeakerDetailViewModel(ConferenceService service) : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Tagline { get; set; } = "";
    [ObservableProperty] public partial string Bio { get; set; } = "";
    [ObservableProperty] public partial ImageSource? Photo { get; set; }
    [ObservableProperty] public partial bool HasPhoto { get; set; }
    [ObservableProperty] public partial string Initials { get; set; } = "";
    [ObservableProperty] public partial string City { get; set; } = "";
    [ObservableProperty] public partial string Error { get; set; } = "";
    public ObservableCollection<SessionItem> Sessions { get; } = [];

    public async Task LoadAsync(string id)
    {
        try
        {
            Error = "";
            var data = await service.GetConferenceAsync();
            var speaker = data.Speakers.First(s => s.Id == id);
            City = data.Cities.First(c => c.Id == service.CityId).Name;
            Name = speaker.Name;
            Tagline = speaker.Tagline ?? "";
            Bio = await service.GetContentAsync(speaker.Content);
            if (string.IsNullOrWhiteSpace(Bio))
                Bio = "No biography has been provided for this speaker.";
            Photo = service.Image(speaker.Image);
            HasPhoto = speaker.Image is not null;
            Initials = string.Concat(speaker.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => s[0]));
            Sessions.Clear();
            foreach (var session in data.Sessions.Where(s => s.CityId == service.CityId && s.SpeakerIds.Contains(id)).OrderBy(s => s.Start))
                Sessions.Add(new SessionItem(session, data, service));
        }
        catch (Exception ex) { Error = $"Unable to load speaker: {ex.Message}"; }
    }
}
