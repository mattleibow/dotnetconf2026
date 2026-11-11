using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public record SessionItem(Session Session, string Speakers);

public partial class AgendaViewModel : ObservableObject
{
    private readonly ConferenceDatabase database;
    private readonly UserPreferencesService preferences;

    public AgendaViewModel(ConferenceDatabase database, UserPreferencesService preferences)
    {
        this.database = database;
        this.preferences = preferences;
        preferences.CityChanged += async (_, _) => await LoadAsync();
        preferences.FavoritesChanged += async (_, _) => await LoadAsync();
    }

    public ObservableCollection<SessionItem> Sessions { get; } = [];

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterText))]
    public partial bool ShowFavoritesOnly { get; set; }

    public string FilterText => ShowFavoritesOnly ? "Showing: Favourites" : "Showing: All Sessions";

    partial void OnShowFavoritesOnlyChanged(bool value) =>
        _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var city = await preferences.GetSelectedCityAsync(database);
        SelectedCity = city;
        var favoriteIds = preferences.GetFavoriteIds();
        Sessions.Clear();
        foreach (var session in await database.GetSessionsAsync(city.Id))
        {
            if (ShowFavoritesOnly && !favoriteIds.Contains(session.Id))
                continue;
            var speakers = await database.GetSpeakersForSessionAsync(session);
            Sessions.Add(new SessionItem(session, string.Join(", ", speakers.Select(s => s.Name))));
        }
    }

    [RelayCommand]
    private void ToggleFavoritesFilter() =>
        ShowFavoritesOnly = !ShowFavoritesOnly;

    [RelayCommand]
    private Task PickCityAsync() =>
        preferences.PickCityAsync(database);

    [RelayCommand]
    private Task OpenSessionAsync(SessionItem item) =>
        Shell.Current.GoToAsync($"session?id={item.Session.Id}");
}
