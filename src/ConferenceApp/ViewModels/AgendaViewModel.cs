using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.AI;
using ConferenceApp.Data;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class AgendaViewModel(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    SearchService search,
    IDispatcher dispatcher) : BaseViewModel(dbProvider, preferences, dispatcher),
    IRecipient<FavoritesChangedMessage>,
    IRecipient<SessionsEnrichedMessage>
{
    private int loadVersion;

    public ObservableCollection<Session> Sessions { get; } = [];

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowFavoritesOnly { get; set; }

    partial void OnSearchQueryChanged(string value) =>
        Refresh();

    partial void OnShowFavoritesOnlyChanged(bool value) =>
        Refresh();

    public void Receive(FavoritesChangedMessage message) =>
        Refresh();

    public void Receive(SessionsEnrichedMessage message) =>
        Refresh();

    protected override async Task OnLoadAsync()
    {
        if (SelectedCity is not { } city)
            return;

        var version = Interlocked.Increment(ref loadVersion);
        var query = SearchQuery;
        var favoritesOnly = ShowFavoritesOnly;
        var favoriteIds = Preferences.GetFavoriteIds();
        var matching = await search.SearchSessionsAsync(city.Id, query);
        if (version != Volatile.Read(ref loadVersion))
            return;

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
    private Task OpenSessionAsync(Session session) =>
        Shell.Current.GoToAsync($"session?id={session.Id}");
}
