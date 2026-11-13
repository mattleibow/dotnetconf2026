using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.AI;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SpeakerViewModel : ObservableObject,
    IQueryAttributable,
    IRecipient<CityChangedMessage>,
    IRecipient<SessionsEnrichedMessage>
{
    private readonly ConferenceDbProvider dbProvider;
    private readonly UserPreferencesService preferences;
    private readonly IDispatcher dispatcher;

    public SpeakerViewModel(
        ConferenceDbProvider dbProvider,
        UserPreferencesService preferences,
        IDispatcher dispatcher)
    {
        this.dbProvider = dbProvider;
        this.preferences = preferences;
        this.dispatcher = dispatcher;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<SessionItem> Sessions { get; } = [];

    [ObservableProperty]
    public partial Speaker? Speaker { get; set; }

    [ObservableProperty]
    public partial string Bio { get; set; } = "";

    public void Receive(CityChangedMessage message) =>
        dispatcher.Dispatch(() => _ = LoadSessionsAsync());

    public void Receive(SessionsEnrichedMessage message) =>
        dispatcher.Dispatch(() => _ = LoadSessionsAsync());

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var db = await dbProvider.GetDatabaseAsync();
        var id = (string)query["id"];
        Speaker = db.Speakers.FirstOrDefault(s => s.Id == id);
        if (Speaker is null)
            return;
        Bio = Speaker.Bio ?? "";
        await LoadSessionsAsync();
    }

    public async Task LoadSessionsAsync()
    {
        if (Speaker is null)
            return;
        var db = await dbProvider.GetDatabaseAsync();
        var city = await preferences.GetSelectedCityAsync();
        var favoriteIds = preferences.GetFavoriteIds();
        Sessions.Clear();
        foreach (var session in db.Sessions.Where(s => s.CityId == city.Id && s.SpeakerIds.Contains(Speaker.Id)))
        {
            Sessions.Add(new SessionItem(
                session,
                favoriteIds.Contains(session.Id),
                city.Name));
        }
    }

    [RelayCommand]
    private Task OpenSessionAsync(SessionItem item) =>
        Shell.Current.GoToAsync($"session?id={item.Session.Id}");
}
