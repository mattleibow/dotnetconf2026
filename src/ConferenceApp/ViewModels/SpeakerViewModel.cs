using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.AI;
using ConferenceApp.Data;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SpeakerViewModel(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IDispatcher dispatcher) : BaseViewModel(dbProvider, preferences, dispatcher),
    IQueryAttributable,
    IRecipient<SessionsEnrichedMessage>
{
    private string? speakerId;

    public ObservableCollection<Session> Sessions { get; } = [];

    [ObservableProperty]
    public partial Speaker? Speaker { get; set; }

    [ObservableProperty]
    public partial string Bio { get; set; } = "";

    public void Receive(SessionsEnrichedMessage message) =>
        Refresh();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        speakerId = (string)query["id"];
        Refresh();
    }

    protected override async Task OnLoadAsync()
    {
        if (string.IsNullOrEmpty(speakerId) || SelectedCity is not { } city)
            return;

        var db = await DbProvider.GetDbAsync();
        Speaker = db.Speakers.FirstOrDefault(s => s.Id == speakerId);
        if (Speaker is null)
            return;

        Bio = Speaker.Bio ?? "";
        Sessions.Clear();
        foreach (var session in db.Sessions.Where(s => s.CityId == city.Id && s.SpeakerIds.Contains(Speaker.Id)))
            Sessions.Add(session);
    }

    [RelayCommand]
    private Task OpenSessionAsync(Session session) =>
        Shell.Current.GoToAsync($"session?id={session.Id}");
}
