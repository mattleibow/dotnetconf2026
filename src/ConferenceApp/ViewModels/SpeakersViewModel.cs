using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SpeakersViewModel(ConferenceDatabase database, UserPreferencesService preferences) : ObservableObject
{
    public ObservableCollection<Speaker> Speakers { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        Speakers.Clear();
        foreach (var speaker in await database.GetSpeakersAsync(preferences.SelectedCityId))
            Speakers.Add(speaker);
    }

    [RelayCommand]
    private Task OpenSpeakerAsync(Speaker speaker) =>
        Shell.Current.GoToAsync($"speaker?id={speaker.Id}");
}
