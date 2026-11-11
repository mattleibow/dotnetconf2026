using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SessionViewModel(ConferenceService data, FavoritesService favorites) : ObservableObject, IQueryAttributable
{
    private Session? session;
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Time { get; set; } = "";
    [ObservableProperty] public partial string Track { get; set; } = "";
    [ObservableProperty] public partial string Abstract { get; set; } = "";
    [ObservableProperty] public partial string FavoriteText { get; set; } = "";
    [ObservableProperty] public partial string Status { get; set; } = "Loading...";
    public ObservableCollection<SpeakerCard> Speakers { get; } = [];

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            var id = Uri.UnescapeDataString((string)query["id"]);
            var conference = await data.GetConferenceAsync();
            session = conference.Sessions.First(s => s.Id == id);
            Title = session.Title;
            Time = $"{session.Start:dddd, d MMMM • HH:mm} – {session.End:HH:mm}";
            Track = session.Track ?? "Conference";
            var content = await data.GetContentAsync(session.Content);
            Abstract = string.IsNullOrWhiteSpace(content) ? "No description is available for this session." : content;
            FavoriteText = favorites.Contains(id) ? "♥  Saved to favourites" : "♡  Add to favourites";
            Speakers.Clear();
            foreach (var speaker in conference.Speakers.Where(s => session.SpeakerIds.Contains(s.Id)))
                Speakers.Add(new SpeakerCard(speaker));
            Status = "";
        }
        catch (Exception ex)
        {
            Status = $"Couldn't load the session: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (session is null)
            return;
        favorites.Toggle(session.Id);
        FavoriteText = favorites.Contains(session.Id) ? "♥  Saved to favourites" : "♡  Add to favourites";
    }

    [RelayCommand]
    private Task OpenSpeakerAsync(SpeakerCard speaker) =>
        Shell.Current.GoToAsync($"speaker?id={Uri.EscapeDataString(speaker.Speaker.Id)}");
}

public sealed class SpeakerCard(Speaker speaker)
{
    public Speaker Speaker { get; } = speaker;
    public string Name => Speaker.Name;
    public string? Tagline => Speaker.Tagline;
    public ImageSource? Photo => ConferenceService.Image(Speaker.Image);
    public string Initials => string.Concat(Speaker.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => part[0]));
    public string AutomationId => $"speaker-{Speaker.Id}";
}
