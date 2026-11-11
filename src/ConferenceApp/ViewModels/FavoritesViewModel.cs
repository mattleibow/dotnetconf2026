using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class FavoritesViewModel(ConferenceService service) : ObservableObject
{
    public ObservableCollection<SessionItem> Sessions { get; } = [];
    [ObservableProperty] public partial string Error { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string City { get; set; } = "";

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Error = "";
            var data = await service.GetConferenceAsync();
            City = data.Cities.First(c => c.Id == service.CityId).Name;
            Sessions.Clear();
            foreach (var session in data.Sessions.Where(s => s.CityId == service.CityId && service.IsFavorite(s.Id)).OrderBy(s => s.Start))
                Sessions.Add(new SessionItem(session, data, service, item => Sessions.Remove(item)));
        }
        catch (Exception ex) { Error = $"Unable to load favourites: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
