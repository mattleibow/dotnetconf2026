using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SpeakersViewModel : ObservableObject, IRecipient<CityChangedMessage>
{
    private readonly ConferenceDbProvider dbProvider;
    private readonly UserPreferencesService preferences;

    public SpeakersViewModel(ConferenceDbProvider dbProvider, UserPreferencesService preferences)
    {
        this.dbProvider = dbProvider;
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
        var db = await dbProvider.GetDatabaseAsync();
        var city = await preferences.GetSelectedCityAsync();
        SelectedCity = city;
        Speakers.Clear();
        foreach (var speaker in db.Speakers.Where(s => s.CityIds.Contains(city.Id)))
            Speakers.Add(speaker);
    }

    [RelayCommand]
    private Task PickCityAsync() =>
        preferences.PickCityAsync();

    [RelayCommand]
    private Task OpenSpeakerAsync(Speaker speaker) =>
        Shell.Current.GoToAsync($"speaker?id={speaker.Id}");
}
