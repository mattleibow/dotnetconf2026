using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SponsorViewModel(ConferenceService data) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Tier { get; set; } = "";
    [ObservableProperty] public partial ImageSource? Logo { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "Loading...";

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            var id = Uri.UnescapeDataString((string)query["id"]);
            var sponsor = (await data.GetConferenceAsync()).Sponsors.First(s => s.Id == id);
            Name = sponsor.Name;
            Tier = sponsor.Tier;
            Logo = ConferenceService.Image(sponsor.Image);
            Status = "";
        }
        catch (Exception ex)
        {
            Status = $"Couldn't load the sponsor: {ex.Message}";
        }
    }
}
