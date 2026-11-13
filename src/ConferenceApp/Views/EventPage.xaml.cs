using System.ComponentModel;
using ConferenceApp.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace ConferenceApp.Views;

public partial class EventPage : BasePage
{
    private readonly EventViewModel viewModel;

    public EventPage(EventViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

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
