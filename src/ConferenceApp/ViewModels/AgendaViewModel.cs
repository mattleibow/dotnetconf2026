using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class AgendaViewModel : ObservableObject
{
    private readonly ConferenceService data;
    private readonly CityService city;
    private readonly FavoritesService favorites;
    private readonly NearbyCityService nearby;
    private readonly SessionSearchService search;
    private Conference? conference;
    private int refreshVersion;

    [ObservableProperty] public partial string CityName { get; set; } = "";
    [ObservableProperty] public partial string EventDate { get; set; } = "";
    [ObservableProperty] public partial string Status { get; set; } = "Loading the agenda...";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsWide { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";

    public ObservableCollection<SessionGroup> Groups { get; } = [];

    partial void OnIsWideChanged(bool value) => _ = RefreshAsync();
    partial void OnSearchTextChanged(string value) => _ = RefreshAsync();

    public AgendaViewModel(ConferenceService data, CityService city, FavoritesService favorites, NearbyCityService nearby, SessionSearchService search)
    {
        this.data = data;
        this.city = city;
        this.favorites = favorites;
        this.nearby = nearby;
        this.search = search;
        city.Changed += (_, _) => _ = RefreshAsync();
        favorites.Changed += (_, _) => _ = RefreshAsync();
    }

    public async Task LoadAsync()
    {
        if (conference is not null)
            return;
        IsBusy = true;
        try
        {
            conference = await data.GetConferenceAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Status = $"Couldn't load the conference: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAsync()
    {
        if (conference is null)
            return;
        var version = ++refreshVersion;
        var selected = conference.Cities.First(c => c.Id == city.SelectedId);
        CityName = selected.Name;
        EventDate = selected.Date.ToString("dddd, d MMMM yyyy");
        var query = SearchText;
        try
        {
            IReadOnlyList<SessionSearchResult> results;
            if (string.IsNullOrWhiteSpace(query))
            {
                results = conference.Sessions.Where(s => s.CityId == selected.Id)
                    .Select(s => new SessionSearchResult(s, "")).ToList();
            }
            else
            {
                Groups.Clear();
                Status = "Searching sessions...";
                results = await search.SearchSessionsAsync(query);
            }

            if (version != refreshVersion)
                return;
            Groups.Clear();
            foreach (var group in results.OrderBy(r => r.Session.Start).GroupBy(r => r.Session.Start))
                Groups.Add(new SessionGroup(group.Key.ToString("HH:mm"), group.Select(r =>
                    new SessionCard(r.Session, conference.Speakers, favorites.Contains(r.Session.Id), IsWide && group.Count() > 1, match: r.Match))));
            Status = Groups.Count == 0
                ? (string.IsNullOrWhiteSpace(query) ? "No sessions available for this city." : "No sessions match your search.")
                : "";
        }
        catch (Exception ex)
        {
            if (version == refreshVersion)
            {
                Groups.Clear();
                Status = $"Couldn't search sessions: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void SelectCity(string id) => city.Select(id);

    [RelayCommand]
    private async Task FindNearestAsync()
    {
        if (!await Shell.Current.DisplayAlertAsync("Find your nearest city",
                "Your location is used only to choose the nearest conference city. You can always pick a city manually.",
                "Use location", "Not now"))
            return;
        try
        {
            var nearest = await nearby.FindNearestAsync();
            if (nearest is not null)
                city.Select(nearest);
            else
                await Shell.Current.DisplayAlertAsync("Location unavailable", "Please choose a city manually. You can enable location in Settings.", "OK");
        }
        catch (Exception ex) when (ex is PermissionException or FeatureNotSupportedException or FeatureNotEnabledException or OperationCanceledException)
        {
            await Shell.Current.DisplayAlertAsync("Location unavailable", "Please choose a city manually. Check your device location settings if you'd like to try again.", "OK");
        }
    }

    [RelayCommand]
    private void ToggleFavorite(SessionCard card) => favorites.Toggle(card.Session.Id);

    [RelayCommand]
    private Task OpenSessionAsync(SessionCard card) =>
        Shell.Current.GoToAsync($"session?id={Uri.EscapeDataString(card.Session.Id)}");
}

public sealed class SessionGroup(string time, IEnumerable<SessionCard> sessions) : ObservableCollection<SessionCard>(sessions)
{
    public string Time { get; } = time;
}

public sealed class SessionCard(Session session, IEnumerable<Speaker> speakers, bool favorite, bool useHalfWidth = false, string cityName = "", string match = "")
{
    public Session Session { get; } = session;
    public string Title => Session.Title;
    public string Time => $"{Session.Start:HH:mm} – {Session.End:HH:mm}";
    public string CityAndDate => $"{cityName} · {Session.Start:d MMMM yyyy}";
    public string Track => Session.Track ?? (Session.Kind == "general" ? "EVENT" : "SESSION");
    public bool IsGeneral => Session.Kind == "general";
    public bool UseHalfWidth => useHalfWidth;
    public string SpeakerNames { get; } = string.Join(", ", speakers.Where(s => session.SpeakerIds.Contains(s.Id)).Select(s => s.Name));
    public string Match { get; } = match;
    public bool HasMatch => !string.IsNullOrEmpty(Match);
    public List<SpeakerCard> Speakers { get; } = speakers.Where(s => session.SpeakerIds.Contains(s.Id)).Select(s => new SpeakerCard(s)).ToList();
    public string Heart => favorite ? "♥" : "♡";
    public string AutomationId => $"session-{Session.Id}";
    public string FavoriteAutomationId => $"favorite-{Session.Id}";
    public string FavoriteDescription => $"{(favorite ? "Remove from" : "Add to")} favourites: {Session.Title}";
}
