using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SponsorDetailViewModel(ConferenceService service) : ObservableObject
{
    private string? url;
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Tier { get; set; } = "";
    [ObservableProperty] public partial string City { get; set; } = "";
    [ObservableProperty] public partial ImageSource? Logo { get; set; }
    [ObservableProperty] public partial bool HasWebsite { get; set; }
    [ObservableProperty] public partial string Error { get; set; } = "";

    public async Task LoadAsync(string name)
    {
        try
        {
            var data = await service.GetConferenceAsync();
            var city = data.Cities.First(c => c.Id == service.CityId);
            var sponsor = data.Sponsors.Single(s => s.CityIds.Contains(city.Id) && s.Name == name);
            Name = sponsor.Name;
            Tier = sponsor.Tier;
            City = city.Name;
            Logo = service.Image(sponsor.Image);
            url = sponsor.Url;
            HasWebsite = !string.IsNullOrWhiteSpace(url);
            Error = "";
        }
        catch (Exception ex) { Error = $"Unable to load sponsor: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task OpenWebsiteAsync()
    {
        if (url is null) return;
        try { await Launcher.Default.OpenAsync(url); }
        catch (Exception ex) { await Shell.Current.DisplayAlertAsync("Unable to open website", ex.Message, "OK"); }
    }
}
