using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public record SessionItem(Session Session, string Speakers);

public partial class AgendaViewModel : ObservableObject, IRecipient<CityChangedMessage>, IRecipient<FavoritesChangedMessage>
{
    private readonly ConferenceDatabase database;
    private readonly UserPreferencesService preferences;
    private readonly SearchService search;

    public AgendaViewModel(ConferenceDatabase database, UserPreferencesService preferences, SearchService search)
    {
        this.database = database;
        this.preferences = preferences;
        this.search = search;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<SessionItem> Sessions { get; } = [];

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterText))]
    public partial bool ShowFavoritesOnly { get; set; }

    public string FilterText => ShowFavoritesOnly
        ? "Showing: Favourites"
        : "Showing: All Sessions";

    partial void OnSearchQueryChanged(string value) =>
        _ = LoadAsync();

    partial void OnShowFavoritesOnlyChanged(bool value) =>
        _ = LoadAsync();

    public void Receive(CityChangedMessage message) =>
        _ = LoadAsync();

    public void Receive(FavoritesChangedMessage message) =>
        _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var city = await preferences.GetSelectedCityAsync(database);
        SelectedCity = city;
        var favoriteIds = preferences.GetFavoriteIds();
        var matching = await search.SearchSessionsAsync(city.Id, SearchQuery);
        Sessions.Clear();
        foreach (var session in matching)
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
