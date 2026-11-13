using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SessionViewModel(ConferenceDbProvider dbProvider, UserPreferencesService preferences) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<Speaker> Speakers { get; } = [];

    [ObservableProperty]
    public partial Session? Session { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var db = await dbProvider.GetDatabaseAsync();
        var id = (string)query["id"];
        Session = db.Sessions.FirstOrDefault(s => s.Id == id);
        if (Session is null)
            return;
        Description = Session.Description ?? "";
        IsFavorite = preferences.IsFavorite(Session.Id);
        Speakers.Clear();
        foreach (var speaker in Session.Speakers)
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
