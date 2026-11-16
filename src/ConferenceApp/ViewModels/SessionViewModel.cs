using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.ViewModels;

public partial class SessionViewModel(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IDispatcher dispatcher,
    ILogger<SessionViewModel> logger) : BaseViewModel(dbProvider, preferences, dispatcher, logger),
    IQueryAttributable,
    IRecipient<FavoritesChangedMessage>
{
    private string? sessionId;

    public ObservableCollection<Speaker> Speakers { get; } = [];

    [ObservableProperty]
    public partial Session? Session { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public void Receive(FavoritesChangedMessage message) =>
        Refresh();

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
