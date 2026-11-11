using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class EventViewModel : ObservableObject, IRecipient<CityChangedMessage>
{
    private readonly ConferenceDatabase database;
    private readonly UserPreferencesService preferences;

    public EventViewModel(ConferenceDatabase database, UserPreferencesService preferences)
    {
        this.database = database;
        this.preferences = preferences;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<Sponsor> Sponsors { get; } = [];

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    [ObservableProperty]
    public partial Venue? Venue { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    public void Receive(CityChangedMessage message) =>
        _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var city = await preferences.GetSelectedCityAsync(database);
        SelectedCity = city;
        Venue = await database.GetVenueAsync(city.Id);
        Description = await database.GetTextAsync(Venue.Content);
        Sponsors.Clear();
        foreach (var sponsor in await database.GetSponsorsAsync(city.Id))
            Sponsors.Add(sponsor);
    }

    [RelayCommand]
    private Task PickCityAsync() => preferences.PickCityAsync(database);

    [RelayCommand]
    private Task OpenMapAsync() =>
        Venue is null ? Task.CompletedTask : Launcher.Default.OpenAsync(Venue.MapUrl);

    [RelayCommand]
    private Task OpenSponsorAsync(Sponsor sponsor) =>
        Launcher.Default.OpenAsync(sponsor.Url);
}
