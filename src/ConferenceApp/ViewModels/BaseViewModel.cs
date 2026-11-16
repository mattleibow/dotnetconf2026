using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Data;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public abstract partial class BaseViewModel : ObservableObject,
    IRecipient<CityChangedMessage>
{
    protected BaseViewModel(
        ConferenceDbProvider dbProvider,
        UserPreferencesService preferences,
        IDispatcher dispatcher)
    {
        DbProvider = dbProvider;
        Preferences = preferences;
        Dispatcher = dispatcher;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    protected ConferenceDbProvider DbProvider { get; }
    protected UserPreferencesService Preferences { get; }
    protected IDispatcher Dispatcher { get; }

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    public void Receive(CityChangedMessage message) =>
        Refresh();

    protected void Refresh() =>
        Dispatcher.Dispatch(() => _ = LoadAsync());

    [RelayCommand]
    private async Task LoadAsync()
    {
        SelectedCity = await Preferences.GetSelectedCityAsync();
        await OnLoadAsync();
    }

    protected abstract Task OnLoadAsync();

    [RelayCommand]
    private async Task PickCityAsync()
    {
        var db = await DbProvider.GetDbAsync();
        var names = db.Cities.Select(c => c.Name).ToArray();
        var choice = await Shell.Current.DisplayActionSheetAsync("Select City", "Cancel", null, names);
        if (db.Cities.FirstOrDefault(c => c.Name == choice) is { } city)
            Preferences.SelectedCityId = city.Id;
    }
}
