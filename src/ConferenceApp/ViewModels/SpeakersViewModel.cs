using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SpeakersViewModel : ObservableObject, IRecipient<CityChangedMessage>
{
    private readonly ConferenceDatabase database;
    private readonly UserPreferencesService preferences;

    public SpeakersViewModel(ConferenceDatabase database, UserPreferencesService preferences)
    {
        this.database = database;
        this.preferences = preferences;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<Speaker> Speakers { get; } = [];

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    public void Receive(CityChangedMessage message) =>
        _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var city = await preferences.GetSelectedCityAsync(database);
        SelectedCity = city;
        Speakers.Clear();
        foreach (var speaker in await database.GetSpeakersAsync(city.Id))
            Speakers.Add(speaker);
    }

    [RelayCommand]
    private Task PickCityAsync() =>
        preferences.PickCityAsync(database);

    [RelayCommand]
    private Task OpenSpeakerAsync(Speaker speaker) =>
        Shell.Current.GoToAsync($"speaker?id={speaker.Id}");
}
