using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Data;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class EventViewModel : BaseViewModel
{
    public EventViewModel(
        ConferenceDbProvider dbProvider,
        UserPreferencesService preferences,
        IDispatcher dispatcher)
        : base(dbProvider, preferences, dispatcher)
    {
    }

    public ObservableCollection<Sponsor> Sponsors { get; } = [];

    [ObservableProperty]
    public partial Venue? Venue { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    protected override async Task OnLoadAsync()
    {
        if (SelectedCity is not { } city)
            return;

        var db = await DbProvider.GetDbAsync();
        Venue = city.Venue;
        Description = city.Venue.Description ?? "";
        Sponsors.Clear();
        foreach (var sponsor in db.Sponsors.Where(s => s.CityIds.Contains(city.Id)))
            Sponsors.Add(sponsor);
    }

    [RelayCommand]
    private Task OpenMapAsync() =>
        Venue is null ? Task.CompletedTask : Launcher.Default.OpenAsync(Venue.MapUrl);

    [RelayCommand]
    private Task OpenSponsorAsync(Sponsor sponsor) =>
        Launcher.Default.OpenAsync(sponsor.Url);
}
