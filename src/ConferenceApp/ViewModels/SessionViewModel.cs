using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SessionViewModel(ConferenceDatabase database, UserPreferencesService preferences) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<Speaker> Speakers { get; } = [];

    [ObservableProperty]
    public partial Session? Session { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteText))]
    public partial bool IsFavorite { get; set; }

    public string FavoriteText => IsFavorite ? "Remove from favourites" : "Add to favourites";

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        Session = await database.GetSessionAsync((string)query["id"]);
        if (Session is null)
            return;
        Description = await database.GetTextAsync(Session.Content);
        IsFavorite = preferences.IsFavorite(Session.Id);
        Speakers.Clear();
        foreach (var speaker in await database.GetSpeakersForSessionAsync(Session))
            Speakers.Add(speaker);
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (Session is null)
            return;
        IsFavorite = !IsFavorite;
        preferences.SetFavorite(Session.Id, IsFavorite);
    }

    [RelayCommand]
    private Task OpenSpeakerAsync(Speaker speaker) =>
        Shell.Current.GoToAsync($"speaker?id={speaker.Id}");
}
