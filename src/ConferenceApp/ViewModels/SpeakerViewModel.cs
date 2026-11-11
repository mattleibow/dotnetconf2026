using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SpeakerViewModel(ConferenceService data) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Initials { get; set; } = "";
    [ObservableProperty] public partial string Tagline { get; set; } = "";
    [ObservableProperty] public partial string Bio { get; set; } = "";
    [ObservableProperty] public partial ImageSource? Photo { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "Loading...";

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            var id = Uri.UnescapeDataString((string)query["id"]);
            var conference = await data.GetConferenceAsync();
            var speaker = conference.Speakers.First(s => s.Id == id);
            Name = speaker.Name;
            Initials = new SpeakerCard(speaker).Initials;
            Tagline = speaker.Tagline ?? "";
            Photo = ConferenceService.Image(speaker.Image);
            var content = await data.GetContentAsync(speaker.Content);
            Bio = string.IsNullOrWhiteSpace(content) ? "No biography is available for this speaker." : content;
            Status = "";
        }
        catch (Exception ex)
        {
            Status = $"Couldn't load the speaker: {ex.Message}";
        }
    }
}
