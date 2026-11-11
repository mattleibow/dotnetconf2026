using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SpeakersViewModel(ConferenceService service) : ObservableObject
{
    public ObservableCollection<SpeakerItem> Speakers { get; } = [];
    [ObservableProperty] public partial string Error { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string City { get; set; } = "";

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Error = "";
            var data = await service.GetConferenceAsync();
            City = data.Cities.First(c => c.Id == service.CityId).Name;
            Speakers.Clear();
            foreach (var speaker in data.Speakers.Where(s => s.CityIds.Contains(service.CityId)).OrderBy(s => s.Name))
                Speakers.Add(new SpeakerItem(speaker, service));
        }
        catch (Exception ex) { Error = $"Unable to load speakers: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}

public partial class SpeakerItem(Speaker speaker, ConferenceService service) : ObservableObject
{
    public string Name => speaker.Name;
    public string Tagline => speaker.Tagline ?? "";
    public ImageSource? Photo => service.Image(speaker.Image);
    public bool HasPhoto => speaker.Image is not null;
    public bool ShowInitials => !HasPhoto;
    public string Initials => string.Concat(speaker.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => s[0]));
    public string AutomationId => $"speaker-{speaker.Id}";

    [RelayCommand]
    private async Task OpenAsync() => await Shell.Current.GoToAsync($"speaker-detail?id={Uri.EscapeDataString(speaker.Id)}");
}
