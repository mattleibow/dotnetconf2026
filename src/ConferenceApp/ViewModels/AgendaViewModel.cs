using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class AgendaViewModel : ObservableObject
{
    private readonly ConferenceService service;
    private readonly SessionSearchService searchService;
    public AgendaViewModel(ConferenceService service, SessionSearchService searchService, SessionEnrichmentService enrichment)
    {
        this.service = service;
        this.searchService = searchService;
        enrichment.TopicsReady += (_, _) => { if (SelectedTopic != "All topics") _ = LoadAsync(); };
    }

    private int loadVersion;
    public string[] Topics { get; } = ["All topics", "AI", "Mobile", "Web", "Data", "Cloud", "Other"];
    [ObservableProperty] public partial string SelectedTopic { get; set; } = "All topics";
    [ObservableProperty] public partial string CityName { get; set; } = "";
    [ObservableProperty] public partial string Date { get; set; } = "";
    [ObservableProperty] public partial string Error { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string SearchNotice { get; set; } = "";
    public string EmptyMessage => SelectedTopic != "All topics" ? "No sessions found for this topic."
        : string.IsNullOrWhiteSpace(SearchText) ? "No sessions found for this city."
        : string.IsNullOrEmpty(SearchNotice) ? "No matching sessions found."
        : "No keyword matches. Try a shorter term, speaker name, or another city.";
    public ObservableCollection<SessionGroup> Groups { get; } = [];
    public bool IsDurban => service.CityId == "durban";
    public bool IsCapeTown => service.CityId == "cape-town";
    public bool IsBloemfontein => service.CityId == "bloemfontein";

    public async Task LoadAsync()
    {
        var version = ++loadVersion;
        IsBusy = true;
        try
        {
            Error = "";
            var data = await service.GetConferenceAsync();
            var cityId = service.CityId;
            var query = SearchText;
            var results = string.IsNullOrWhiteSpace(query) ? null : await searchService.SearchSessionsAsync(query);
            if (version != loadVersion)
                return;
            var city = data.Cities.First(c => c.Id == cityId);
            CityName = city.Name;
            Date = city.Date.ToString("dddd, d MMMM yyyy");
            OnPropertyChanged(nameof(IsDurban));
            OnPropertyChanged(nameof(IsCapeTown));
            OnPropertyChanged(nameof(IsBloemfontein));
            Groups.Clear();
            SearchNotice = results?.Warning ?? "";
            OnPropertyChanged(nameof(EmptyMessage));
            if (results is null)
            {
                var items = data.Sessions.Where(s => s.CityId == city.Id && (SelectedTopic == "All topics" || s.Topic == SelectedTopic))
                    .Select(s => new SessionItem(s, data, service));
                foreach (var group in items.OrderBy(s => s.Start).GroupBy(s => s.Start))
                    Groups.Add(new SessionGroup(group.Key.ToString("HH:mm"), group));
            }
            else
            {
                foreach (var group in results.Results.Where(r => SelectedTopic == "All topics" || r.Session.Topic == SelectedTopic)
                    .GroupBy(r => r.IsSemantic ? "Semantic matches (nearest, may be unrelated)" : "Keyword matches"))
                    Groups.Add(new SessionGroup(group.Key, group.Select(r => new SessionItem(r.Session, data, service, matchText: r.MatchText))));
            }
        }
        catch (Exception ex) { if (version == loadVersion) Error = $"Unable to load the agenda: {ex.Message}"; }
        finally { if (version == loadVersion) IsBusy = false; }
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(EmptyMessage));
        _ = LoadAsync();
    }

    partial void OnSelectedTopicChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task SelectCityAsync(string id)
    {
        service.CityId = id;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task FindNearestAsync()
    {
        if (!await Shell.Current.DisplayAlertAsync("Find your nearest city", "Your location is used only to choose the nearest conference city. You can always select a city yourself.", "Continue", "Not now"))
            return;
        try
        {
            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15)));
            if (location is null)
            {
                await Shell.Current.DisplayAlertAsync("Location unavailable", "Please choose a city manually.", "OK");
                return;
            }
            var cities = new[] { ("durban", -29.8587, 31.0218), ("cape-town", -33.9249, 18.4241), ("bloemfontein", -29.0852, 26.1596) };
            service.CityId = cities.MinBy(c => Location.CalculateDistance(location.Latitude, location.Longitude, c.Item2, c.Item3, DistanceUnits.Kilometers)).Item1;
            await LoadAsync();
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlertAsync("Location unavailable", "Location permission was denied. Please choose a city manually.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Location unavailable", $"Please choose a city manually. {ex.Message}", "OK");
        }
    }
}

public sealed class SessionGroup(string title, IEnumerable<SessionItem> items) : ObservableCollection<SessionItem>(items)
{
    public string Title { get; } = title;
}

public partial class SessionItem : ObservableObject
{
    private readonly Session session;
    private readonly ConferenceService service;
    private readonly Action<SessionItem>? favoriteChanged;
    public string Title => session.Title;
    public Session Session => session;
    public DateTimeOffset Start => session.Start;
    public string MatchText { get; }
    public bool HasMatchText => !string.IsNullOrEmpty(MatchText);
    public string Time => $"{session.Start:HH:mm} – {session.End:HH:mm}";
    public string Track => session.Track ?? "Main session";
    public string Speakers { get; }
    public List<SpeakerItem> SpeakerItems { get; }
    public bool IsGeneral => !IsSession;
    public bool IsSession => session.Kind == "session";
    public string AutomationId => $"session-{session.Id}";
    public string TopicAutomationId => $"topic-{session.Id}";
    public string SummaryAutomationId => $"summary-{session.Id}";
    public string MatchAutomationId => $"search-match-{session.Id}";
    public string FavoriteAutomationId => $"favorite-{session.Id}";
    public string FavoriteDescription => service.IsFavorite(session.Id) ? $"Remove {session.Title} from favourites" : $"Add {session.Title} to favourites";
    public string Heart => service.IsFavorite(session.Id) ? "heart_filled.png" : "heart_outline.png";

    public SessionItem(Session session, Conference data, ConferenceService service, Action<SessionItem>? favoriteChanged = null, string matchText = "")
    {
        this.session = session;
        this.service = service;
        this.favoriteChanged = favoriteChanged;
        MatchText = matchText;
        Speakers = string.Join(", ", data.Speakers.Where(s => session.SpeakerIds.Contains(s.Id)).Select(s => s.Name));
        SpeakerItems = data.Speakers.Where(s => session.SpeakerIds.Contains(s.Id)).Select(s => new SpeakerItem(s, service)).ToList();
    }

    [RelayCommand]
    private async Task OpenAsync() => await Shell.Current.GoToAsync($"session-detail?id={Uri.EscapeDataString(session.Id)}");

    [RelayCommand]
    private void Favorite()
    {
        service.ToggleFavorite(session.Id);
        OnPropertyChanged(nameof(Heart));
        OnPropertyChanged(nameof(FavoriteDescription));
        favoriteChanged?.Invoke(this);
    }
}
