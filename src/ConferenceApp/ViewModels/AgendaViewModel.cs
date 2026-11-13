using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.AI;
using ConferenceApp.Data;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class AgendaViewModel : ObservableObject,
    IRecipient<CityChangedMessage>,
    IRecipient<FavoritesChangedMessage>,
    IRecipient<SessionsEnrichedMessage>
{
    private readonly UserPreferencesService preferences;
    private readonly SearchService search;
    private readonly IDispatcher dispatcher;
    private int loadVersion;

    public AgendaViewModel(
        UserPreferencesService preferences,
        SearchService search,
        IDispatcher dispatcher)
    {
        this.preferences = preferences;
        this.search = search;
        this.dispatcher = dispatcher;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<Session> Sessions { get; } = [];

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowFavoritesOnly { get; set; }

    partial void OnSearchQueryChanged(string value) =>
        _ = LoadAsync();

    partial void OnShowFavoritesOnlyChanged(bool value) =>
        _ = LoadAsync();

    public void Receive(CityChangedMessage message) =>
        dispatcher.Dispatch(() => _ = LoadAsync());

    public void Receive(FavoritesChangedMessage message) =>
        dispatcher.Dispatch(() => _ = LoadAsync());

    public void Receive(SessionsEnrichedMessage message) =>
        dispatcher.Dispatch(() => _ = LoadAsync());

    [RelayCommand]
    private async Task LoadAsync()
    {
        var version = Interlocked.Increment(ref loadVersion);
        var query = SearchQuery;
        var favoritesOnly = ShowFavoritesOnly;
        var city = await preferences.GetSelectedCityAsync();
        var favoriteIds = preferences.GetFavoriteIds();
        var matching = await search.SearchSessionsAsync(city.Id, query);
        if (version != Volatile.Read(ref loadVersion))
            return;

        SelectedCity = city;
        Sessions.Clear();
        foreach (var session in matching)
        {
            if (favoritesOnly && !favoriteIds.Contains(session.Id))
                continue;
            Sessions.Add(session);
        }
    }

    [RelayCommand]
    private void ToggleFavoritesFilter() =>
        ShowFavoritesOnly = !ShowFavoritesOnly;

    [RelayCommand]
    private Task PickCityAsync() =>
        preferences.PickCityAsync();

    [RelayCommand]
    private Task OpenSessionAsync(Session session) =>
        Shell.Current.GoToAsync($"session?id={session.Id}");
}
