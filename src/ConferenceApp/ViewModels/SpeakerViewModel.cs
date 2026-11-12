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
    IRecipient<SessionEnrichedMessage>
{
    private readonly ConferenceDatabase database;
    private readonly UserPreferencesService preferences;
    private readonly SessionEnrichmentService enrichment;

    public SpeakerViewModel(ConferenceDatabase database, UserPreferencesService preferences, SessionEnrichmentService enrichment)
    {
        this.database = database;
        this.preferences = preferences;
        this.enrichment = enrichment;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<SessionItem> Sessions { get; } = [];

    [ObservableProperty]
    public partial Speaker? Speaker { get; set; }

    [ObservableProperty]
    public partial string Bio { get; set; } = "";

    public void Receive(CityChangedMessage message) =>
        _ = LoadSessionsAsync();

    public void Receive(SessionEnrichedMessage message) =>
        MainThread.BeginInvokeOnMainThread(() => ApplyEnrichment(message.SessionId));

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
            var enriched = enrichment.GetSession(session.Id);
            Sessions.Add(new SessionItem(
                session,
                Speaker.Name,
                favoriteIds.Contains(session.Id),
                enriched.Summary,
                enriched.HasTopic ? enriched.Topic : null,
                city.Name));
        }
    }

    private void ApplyEnrichment(string? sessionId)
    {
        foreach (var item in Sessions.Where(item => sessionId is null || item.Session.Id == sessionId))
        {
            var enriched = enrichment.GetSession(item.Session.Id);
            item.Summary = enriched.Summary;
            item.Topic = enriched.HasTopic ? enriched.Topic : null;
        }
    }

    [RelayCommand]
    private Task OpenSessionAsync(SessionItem item) =>
        Shell.Current.GoToAsync($"session?id={item.Session.Id}");
}
