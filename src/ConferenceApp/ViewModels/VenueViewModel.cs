using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class VenueViewModel(ConferenceDatabase database, UserPreferencesService preferences) : ObservableObject
{
    public ObservableCollection<Sponsor> Sponsors { get; } = [];

    [ObservableProperty]
    public partial Venue? Venue { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        var city = await preferences.GetSelectedCityAsync(database);
        Venue = await database.GetVenueAsync(city.Id);
        Description = await database.GetTextAsync(Venue.Content);
        Sponsors.Clear();
        foreach (var sponsor in await database.GetSponsorsAsync(city.Id))
            Sponsors.Add(sponsor);
    }

    [RelayCommand]
    private Task OpenMapAsync() =>
        Venue is null ? Task.CompletedTask : Launcher.Default.OpenAsync(Venue.MapUrl);

    [RelayCommand]
    private Task OpenSponsorAsync(Sponsor sponsor) =>
        Launcher.Default.OpenAsync(sponsor.Url);
}
