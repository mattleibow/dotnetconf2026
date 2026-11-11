using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Models;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public record SessionItem(Session Session, string Speakers);

public partial class AgendaViewModel(ConferenceDatabase database, UserPreferencesService preferences) : ObservableObject
{
    public ObservableCollection<City> Cities { get; } = [];
    public ObservableCollection<SessionItem> Sessions { get; } = [];

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    partial void OnSelectedCityChanged(City? value)
    {
        if (value is null)
            return;
        _ = LoadSessionsAsync(value.Id);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (Cities.Count == 0)
        {
            foreach (var city in await database.GetCitiesAsync())
                Cities.Add(city);
        }
        SelectedCity = await preferences.GetSelectedCityAsync(database);
    }

    private async Task LoadSessionsAsync(string cityId)
    {
        Sessions.Clear();
        foreach (var session in await database.GetSessionsAsync(cityId))
        {
            var speakers = await database.GetSpeakersForSessionAsync(session);
            Sessions.Add(new SessionItem(session, string.Join(", ", speakers.Select(s => s.Name))));
        }
    }

    [RelayCommand]
    private async Task PickCityAsync()
    {
        var names = Cities.Select(c => c.Name).ToArray();
        var choice = await Shell.Current.DisplayActionSheetAsync("Select City", "Cancel", null, names);
        if (Cities.FirstOrDefault(c => c.Name == choice) is { } city)
        {
            preferences.SelectedCityId = city.Id;
            SelectedCity = city;
        }
    }

    [RelayCommand]
    private Task OpenSessionAsync(SessionItem item) =>
        Shell.Current.GoToAsync($"session?id={item.Session.Id}");
}
