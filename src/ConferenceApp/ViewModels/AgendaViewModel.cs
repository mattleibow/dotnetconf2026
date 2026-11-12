using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.AI;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class AgendaViewModel : ObservableObject,
    IRecipient<CityChangedMessage>,
    IRecipient<FavoritesChangedMessage>,
    IRecipient<SessionEnrichedMessage>
{
    private readonly ConferenceDatabase database;
    private readonly UserPreferencesService preferences;
    private readonly SearchService search;
    private readonly SessionEnrichmentService enrichment;
    private int loadVersion;

    public AgendaViewModel(ConferenceDatabase database, UserPreferencesService preferences,
        SearchService search, SessionEnrichmentService enrichment)
    {
        this.database = database;
        this.preferences = preferences;
        this.search = search;
        this.enrichment = enrichment;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<SessionItem> Sessions { get; } = [];

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
        _ = LoadAsync();

    public void Receive(FavoritesChangedMessage message) =>
        _ = LoadAsync();

    public void Receive(SessionEnrichedMessage message) =>
        MainThread.BeginInvokeOnMainThread(() => ApplyEnrichment(message.SessionId));

    [RelayCommand]
    private async Task LoadAsync()
    {
        var version = Interlocked.Increment(ref loadVersion);
        var query = SearchQuery;
        var favoritesOnly = ShowFavoritesOnly;
        var city = await preferences.GetSelectedCityAsync(database);
        var favoriteIds = preferences.GetFavoriteIds();
        var matching = await search.SearchSessionsAsync(city.Id, query);
        if (version != Volatile.Read(ref loadVersion))
            return;

        var items = new List<SessionItem>(matching.Count);
        foreach (var session in matching)
        {
            if (favoritesOnly && !favoriteIds.Contains(session.Id))
                continue;
            var speakers = await database.GetSpeakersForSessionAsync(session);
            var enriched = enrichment.GetSession(session.Id);
            items.Add(new SessionItem(
                session,
                string.Join(", ", speakers.Select(s => s.Name)),
                favoriteIds.Contains(session.Id),
                enriched.Summary,
                enriched.HasTopic ? enriched.Topic : null));
        }
        if (version != Volatile.Read(ref loadVersion))
            return;

        SelectedCity = city;
        Sessions.Clear();
        foreach (var item in items)
            Sessions.Add(item);
    }

    private void ApplyEnrichment(string? sessionId)
    {
        foreach (var item in Sessions.Where(item => sessionId is null || item.Session.Id == sessionId))
        {
            var enriched = enrichment.GetSession(item.Session.Id);
            item.Summary = enriched.Summary;
            item.Topic = enriched.HasTopic ? enriched.Topic : null;
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
