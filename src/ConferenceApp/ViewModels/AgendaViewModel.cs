using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public record SessionItem(Session Session, string Speakers);

public partial class AgendaViewModel(ConferenceService conference) : ObservableObject
{
    public ObservableCollection<City> Cities { get; } = [];
    public ObservableCollection<SessionItem> Sessions { get; } = [];

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    partial void OnSelectedCityChanged(City? value)
    {
        if (value is null || value.Id == conference.SelectedCityId)
            return;
        conference.SelectedCityId = value.Id;
        _ = LoadSessionsAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (Cities.Count == 0)
        {
            foreach (var city in await conference.GetCitiesAsync())
                Cities.Add(city);
        }
        SelectedCity = await conference.GetSelectedCityAsync();
        await LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        Sessions.Clear();
        foreach (var session in await conference.GetSessionsAsync())
        {
            var speakers = await conference.GetSpeakersForSessionAsync(session);
            Sessions.Add(new SessionItem(session, string.Join(", ", speakers.Select(s => s.Name))));
        }
    }

    [RelayCommand]
    private Task OpenSessionAsync(SessionItem item) =>
        Shell.Current.GoToAsync($"session?id={item.Session.Id}");
}
