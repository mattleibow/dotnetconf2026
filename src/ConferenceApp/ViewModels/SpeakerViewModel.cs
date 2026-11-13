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
    private readonly ConferenceDatabase database;
    private readonly UserPreferencesService preferences;
    private readonly IDispatcher dispatcher;

    public SpeakerViewModel(
        ConferenceDatabase database,
        UserPreferencesService preferences,
        IDispatcher dispatcher)
    {
        this.database = database;
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
        Speaker = await database.GetSpeakerAsync((string)query["id"]);
        if (Speaker is null)
            return;
        Bio = await database.GetTextAsync(Speaker.Content);
        await LoadSessionsAsync();
    }

    public async Task LoadSessionsAsync()
    {
        if (Speaker is null)
            return;
        var city = await preferences.GetSelectedCityAsync(database);
        var favoriteIds = preferences.GetFavoriteIds();
        Sessions.Clear();
        foreach (var session in await database.GetSessionsForSpeakerAsync(city.Id, Speaker.Id))
        {
            Sessions.Add(new SessionItem(
                session,
                Speaker.Name,
                favoriteIds.Contains(session.Id),
                city.Name));
        }
    }

    [RelayCommand]
    private Task OpenSessionAsync(SessionItem item) =>
        Shell.Current.GoToAsync($"session?id={item.Session.Id}");
}
