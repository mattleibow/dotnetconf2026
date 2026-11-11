using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class ExploreViewModel(ConferenceService data, CityService city) : ObservableObject
{
    private Venue? venue;
    [ObservableProperty] public partial string CityName { get; set; } = "";
    [ObservableProperty] public partial string VenueName { get; set; } = "";
    [ObservableProperty] public partial string Address { get; set; } = "";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial ImageSource? VenueImage { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "Loading...";
    public ObservableCollection<SponsorCard> Sponsors { get; } = [];

    public async Task LoadAsync()
    {
        try
        {
            var conference = await data.GetConferenceAsync();
            var selected = conference.Cities.First(c => c.Id == city.SelectedId);
            venue = conference.Venues.First(v => v.Id == selected.VenueId);
            CityName = selected.Name;
            VenueName = venue.Name;
            Address = venue.Address;
            var description = await data.GetContentAsync(venue.Content);
            Description = description == venue.Address ? "" : description;
            VenueImage = ConferenceService.Image(venue.Image);
            Sponsors.Clear();
            foreach (var sponsor in conference.Sponsors.Where(s => s.CityIds.Contains(selected.Id)))
                Sponsors.Add(new SponsorCard(sponsor));
            Status = "";
        }
        catch (Exception ex)
        {
            Status = $"Couldn't load the venue and sponsors: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenMapAsync()
    {
        if (venue is not null)
            await Launcher.Default.OpenAsync(venue.MapUrl);
    }
}

public sealed class SponsorCard(Sponsor sponsor)
{
    public string Name => sponsor.Name;
    public string Tier => sponsor.Tier;
    public ImageSource? Logo => ConferenceService.Image(sponsor.Image);
}
