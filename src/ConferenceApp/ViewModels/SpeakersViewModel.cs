using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.ViewModels;

public partial class SpeakersViewModel(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IDispatcher dispatcher,
    ILogger<SpeakersViewModel> logger) : BaseViewModel(dbProvider, preferences, dispatcher, logger)
{
    public ObservableCollection<Speaker> Speakers { get; } = [];

    protected override async Task OnLoadAsync()
    {
        if (SelectedCity is not { } city)
            return;

        var db = await DbProvider.GetDbAsync();
        Speakers.Clear();
        foreach (var speaker in db.Speakers.Where(s => s.CityIds.Contains(city.Id)))
            Speakers.Add(speaker);
    }

    [RelayCommand]
    private Task OpenSpeakerAsync(Speaker speaker) =>
        Shell.Current.GoToAsync($"speaker?id={speaker.Id}");
}
