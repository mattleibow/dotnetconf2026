using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.ViewModels;

public partial class SpeakerViewModel(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IDispatcher dispatcher,
    ILogger<SpeakerViewModel> logger) : BaseViewModel(dbProvider, preferences, dispatcher, logger),
    IQueryAttributable
{
    private string? speakerId;

    public ObservableCollection<Session> Sessions { get; } = [];

    [ObservableProperty]
    public partial Speaker? Speaker { get; set; }

    [ObservableProperty]
    public partial string Bio { get; set; } = "";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        query.TryGetValue("speakerId", out var id);
        speakerId = id as string;
        Refresh();
    }

    protected override async Task OnLoadAsync()
    {
        if (SelectedCity is not { } city)
            return;

        var db = await DbProvider.GetDbAsync();
        Speaker = db.Speakers.First(s => s.Id == speakerId);
        Bio = Speaker.Bio ?? "";
        Sessions.Clear();
        foreach (var session in db.Sessions.Where(s => s.CityId == city.Id && s.SpeakerIds.Contains(Speaker.Id)))
            Sessions.Add(session);
    }

    [RelayCommand]
    private Task OpenSessionAsync(Session session) =>
        Shell.Current.GoToAsync($"session?id={session.Id}");
}
