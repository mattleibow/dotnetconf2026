using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SessionDetailViewModel(ConferenceService service) : ObservableObject
{
    private string? id;
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Time { get; set; } = "";
    [ObservableProperty] public partial string Track { get; set; } = "";
    [ObservableProperty] public partial string CityAndDate { get; set; } = "";
    [ObservableProperty] public partial string Abstract { get; set; } = "";
    [ObservableProperty] public partial string FavoriteText { get; set; } = "";
    [ObservableProperty] public partial bool CanFavorite { get; set; }
    [ObservableProperty] public partial string Error { get; set; } = "";
    public ObservableCollection<SpeakerItem> Speakers { get; } = [];

    public async Task LoadAsync(string sessionId)
    {
        try
        {
            id = sessionId;
            Error = "";
            var data = await service.GetConferenceAsync();
            var session = data.Sessions.First(s => s.Id == sessionId);
            Title = session.Title;
            Time = $"{session.Start:HH:mm} – {session.End:HH:mm}";
            Track = session.Track ?? "Main session";
            var city = data.Cities.First(c => c.Id == session.CityId);
            CityAndDate = $"{city.Name} · {city.Date:dddd, d MMMM yyyy}";
            Abstract = await service.GetContentAsync(session.Content);
            if (string.IsNullOrWhiteSpace(Abstract))
                Abstract = "No abstract has been provided for this session.";
            CanFavorite = session.Kind == "session";
            FavoriteText = service.IsFavorite(sessionId) ? "Remove from favourites" : "Add to favourites";
            Speakers.Clear();
            foreach (var speaker in data.Speakers.Where(s => session.SpeakerIds.Contains(s.Id)))
                Speakers.Add(new SpeakerItem(speaker, service));
        }
        catch (Exception ex) { Error = $"Unable to load session: {ex.Message}"; }
    }

    [RelayCommand]
    private void Favorite()
    {
        if (id is null) return;
        service.ToggleFavorite(id);
        FavoriteText = service.IsFavorite(id) ? "Remove from favourites" : "Add to favourites";
    }
}
