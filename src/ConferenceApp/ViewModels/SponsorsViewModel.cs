using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SponsorsViewModel(ConferenceDatabase database, UserPreferencesService preferences) : ObservableObject
{
    public ObservableCollection<Sponsor> Sponsors { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        var city = await preferences.GetSelectedCityAsync(database);
        Sponsors.Clear();
        foreach (var sponsor in await database.GetSponsorsAsync(city.Id))
            Sponsors.Add(sponsor);
    }

    [RelayCommand]
    private Task OpenSponsorAsync(Sponsor sponsor) =>
        Launcher.Default.OpenAsync(sponsor.Url);
}
