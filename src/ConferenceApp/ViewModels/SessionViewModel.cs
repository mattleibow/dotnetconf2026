using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Data;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SessionViewModel(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IDispatcher dispatcher) : BaseViewModel(dbProvider, preferences, dispatcher),
    IQueryAttributable
{
    private string? sessionId;

    public ObservableCollection<Speaker> Speakers { get; } = [];

    [ObservableProperty]
    public partial Session? Session { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        sessionId = (string)query["id"];
        Refresh();
    }

    protected override async Task OnLoadAsync()
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        var db = await DbProvider.GetDbAsync();
        Session = db.Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (Session is null)
            return;

        Description = Session.Description ?? "";
        IsFavorite = Preferences.IsFavorite(Session.Id);
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
        Preferences.SetFavorite(Session.Id, IsFavorite);
    }

    [RelayCommand]
    private Task OpenSpeakerAsync(Speaker speaker) =>
        Shell.Current.GoToAsync($"speaker?id={speaker.Id}");
}
