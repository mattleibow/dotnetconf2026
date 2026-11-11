using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class VenueViewModel(ConferenceService service) : ObservableObject
{
    private Venue? venue;
    [ObservableProperty] public partial string City { get; set; } = "";
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Address { get; set; } = "";
    [ObservableProperty] public partial ImageSource? Photo { get; set; }
    [ObservableProperty] public partial string Error { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    public ObservableCollection<SponsorItem> Sponsors { get; } = [];

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Error = "";
            var data = await service.GetConferenceAsync();
            var city = data.Cities.First(c => c.Id == service.CityId);
            venue = data.Venues.First(v => v.Id == city.VenueId);
            City = city.Name;
            Name = venue.Name;
            Address = venue.Address;
            Photo = service.Image(venue.Image);
            Sponsors.Clear();
            foreach (var sponsor in data.Sponsors.Where(s => s.CityIds.Contains(city.Id)))
                Sponsors.Add(new SponsorItem(sponsor, service));
        }
        catch (Exception ex) { Error = $"Unable to load venue: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task OpenMapAsync()
    {
        if (venue is null) return;
        try { await Launcher.Default.OpenAsync(venue.MapUrl); }
        catch (Exception ex) { await Shell.Current.DisplayAlertAsync("Unable to open map", ex.Message, "OK"); }
    }
}

public sealed partial class SponsorItem(Sponsor sponsor, ConferenceService service)
{
    public string Name => sponsor.Name;
    public string Tier => sponsor.Tier;
    public ImageSource? Logo => service.Image(sponsor.Image);
    public string AutomationId => $"sponsor-{string.Concat(sponsor.Name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-'))}";

    [RelayCommand]
    private async Task OpenAsync() => await Shell.Current.GoToAsync($"sponsor-detail?name={Uri.EscapeDataString(sponsor.Name)}");
}
