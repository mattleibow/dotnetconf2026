using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class SpeakerViewModel(ConferenceService conference) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<Session> Sessions { get; } = [];

    [ObservableProperty]
    public partial Speaker? Speaker { get; set; }

    [ObservableProperty]
    public partial string Bio { get; set; } = "";

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        Speaker = await conference.GetSpeakerAsync((string)query["id"]);
        if (Speaker is null)
            return;
        Bio = await conference.GetTextAsync(Speaker.Content);
        Sessions.Clear();
        foreach (var session in await conference.GetSessionsForSpeakerAsync(Speaker.Id))
            Sessions.Add(session);
    }

    [RelayCommand]
    private Task OpenSessionAsync(Session session) =>
        Shell.Current.GoToAsync($"session?id={session.Id}");
}
