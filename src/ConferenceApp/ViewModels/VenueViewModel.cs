using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class VenueViewModel(ConferenceService conference) : ObservableObject
{
    public ObservableCollection<Sponsor> Sponsors { get; } = [];

    [ObservableProperty]
    public partial Venue? Venue { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        Venue = await conference.GetVenueAsync();
        Description = await conference.GetTextAsync(Venue.Content);
        Sponsors.Clear();
        foreach (var sponsor in await conference.GetSponsorsAsync())
            Sponsors.Add(sponsor);
    }

    [RelayCommand]
    private Task OpenMapAsync() =>
        Venue is null ? Task.CompletedTask : Launcher.Default.OpenAsync(Venue.MapUrl);

    [RelayCommand]
    private Task OpenSponsorAsync(Sponsor sponsor) =>
        Launcher.Default.OpenAsync(sponsor.Url);
}
