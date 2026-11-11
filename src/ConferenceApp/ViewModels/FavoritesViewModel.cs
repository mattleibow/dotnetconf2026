using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class FavoritesViewModel(ConferenceDatabase database, UserPreferencesService preferences) : ObservableObject
{
    public ObservableCollection<SessionItem> Sessions { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        Sessions.Clear();
        var ids = preferences.GetFavoriteIds();
        foreach (var session in (await database.GetSessionsAsync(preferences.SelectedCityId)).Where(s => ids.Contains(s.Id)))
        {
            var speakers = await database.GetSpeakersForSessionAsync(session);
            Sessions.Add(new SessionItem(session, string.Join(", ", speakers.Select(s => s.Name))));
        }
    }

    [RelayCommand]
    private Task OpenSessionAsync(SessionItem item) =>
        Shell.Current.GoToAsync($"session?id={item.Session.Id}");
}
