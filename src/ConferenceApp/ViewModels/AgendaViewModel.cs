using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class AgendaViewModel(ConferenceService service) : ObservableObject
{
    [ObservableProperty] public partial string CityName { get; set; } = "";
    [ObservableProperty] public partial string Date { get; set; } = "";
    [ObservableProperty] public partial string Error { get; set; } = "";
    [ObservableProperty] public partial bool IsBusy { get; set; }
    public ObservableCollection<SessionGroup> Groups { get; } = [];
    public bool IsDurban => service.CityId == "durban";
    public bool IsCapeTown => service.CityId == "cape-town";
    public bool IsBloemfontein => service.CityId == "bloemfontein";

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Error = "";
            var data = await service.GetConferenceAsync();
            var city = data.Cities.First(c => c.Id == service.CityId);
            CityName = city.Name;
            Date = city.Date.ToString("dddd, d MMMM yyyy");
            OnPropertyChanged(nameof(IsDurban));
            OnPropertyChanged(nameof(IsCapeTown));
            OnPropertyChanged(nameof(IsBloemfontein));
            Groups.Clear();
            foreach (var group in data.Sessions.Where(s => s.CityId == city.Id).OrderBy(s => s.Start).GroupBy(s => s.Start))
                Groups.Add(new SessionGroup(group.Key.ToString("HH:mm"), group.Select(s => new SessionItem(s, data, service))));
        }
        catch (Exception ex) { Error = $"Unable to load the agenda: {ex.Message}"; }
        finally { IsBusy = false; }
    }

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
    public string Time => $"{session.Start:HH:mm} – {session.End:HH:mm}";
    public string Track => session.Track ?? "Main session";
    public string Speakers { get; }
    public List<SpeakerItem> SpeakerItems { get; }
    public bool IsGeneral => !IsSession;
    public bool IsSession => session.Kind == "session";
    public string AutomationId => $"session-{session.Id}";
    public string FavoriteAutomationId => $"favorite-{session.Id}";
    public string FavoriteDescription => service.IsFavorite(session.Id) ? $"Remove {session.Title} from favourites" : $"Add {session.Title} to favourites";
    public string Heart => service.IsFavorite(session.Id) ? "heart_filled.png" : "heart_outline.png";

    public SessionItem(Session session, Conference data, ConferenceService service, Action<SessionItem>? favoriteChanged = null)
    {
        this.session = session;
        this.service = service;
        this.favoriteChanged = favoriteChanged;
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
