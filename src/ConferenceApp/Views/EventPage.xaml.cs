using System.ComponentModel;
using ConferenceApp.Services;
using ConferenceApp.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace ConferenceApp.Views;

public partial class EventPage : ContentPage
{
    private readonly EventViewModel viewModel;
    private readonly UserPreferencesService preferences;

    public EventPage(EventViewModel viewModel, UserPreferencesService preferences)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        this.preferences = preferences;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        preferences.CityChanged += OnCityChanged;
        await viewModel.LoadCommand.ExecuteAsync(null);
        UpdateMap();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        preferences.CityChanged -= OnCityChanged;
    }

    private void OnCityChanged(object? sender, EventArgs e) =>
        viewModel.LoadCommand.Execute(null);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EventViewModel.Venue))
            UpdateMap();
    }

    private void UpdateMap()
    {
        if (viewModel.Venue is not { } venue)
            return;

        var location = new Location(venue.Latitude, venue.Longitude);
        VenueMap.Pins.Clear();
        VenueMap.Pins.Add(new Pin
        {
            Label = venue.Name,
            Address = venue.Address,
            Location = location
        });
        VenueMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(0.8)));
    }
}
