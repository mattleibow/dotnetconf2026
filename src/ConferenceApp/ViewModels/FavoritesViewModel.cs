using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class FavoritesViewModel(ConferenceService data, FavoritesService favorites) : ObservableObject
{
    [ObservableProperty] public partial string Status { get; set; } = "Loading favourites...";
    public ObservableCollection<SessionCard> Sessions { get; } = [];

    public async Task LoadAsync()
    {
        try
        {
            var conference = await data.GetConferenceAsync();
            Sessions.Clear();
            foreach (var session in conference.Sessions.Where(s => favorites.Contains(s.Id)).OrderBy(s => s.Start))
                Sessions.Add(new SessionCard(session, conference.Speakers, true,
                    cityName: conference.Cities.First(c => c.Id == session.CityId).Name));
            Status = Sessions.Count == 0 ? "Your favourite sessions will appear here." : "";
        }
        catch (Exception ex)
        {
            Status = $"Couldn't load favourites: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task OpenSessionAsync(SessionCard card) =>
        Shell.Current.GoToAsync($"session?id={Uri.EscapeDataString(card.Session.Id)}");
}
