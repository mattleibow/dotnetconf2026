using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class EventViewModel : ObservableObject, IRecipient<CityChangedMessage>
{
    private readonly ConferenceDbProvider dbProvider;
    private readonly UserPreferencesService preferences;

    public EventViewModel(ConferenceDbProvider dbProvider, UserPreferencesService preferences)
    {
        this.dbProvider = dbProvider;
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
        var db = await dbProvider.GetDatabaseAsync();
        var city = await preferences.GetSelectedCityAsync();
        SelectedCity = city;
        Venue = city.Venue;
        Description = city.Venue.Description ?? "";
        Sponsors.Clear();
        foreach (var sponsor in db.Sponsors.Where(s => s.CityIds.Contains(city.Id)))
            Sponsors.Add(sponsor);
    }

    [RelayCommand]
    private Task PickCityAsync() => preferences.PickCityAsync();

    [RelayCommand]
    private Task OpenMapAsync() =>
        Venue is null ? Task.CompletedTask : Launcher.Default.OpenAsync(Venue.MapUrl);

    [RelayCommand]
    private Task OpenSponsorAsync(Sponsor sponsor) =>
        Launcher.Default.OpenAsync(sponsor.Url);
}
