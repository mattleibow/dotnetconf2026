using ConferenceApp.Services;
using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class AgendaPage : ContentPage
{
    private readonly AgendaViewModel viewModel;
    private readonly UserPreferencesService preferences;

    public AgendaPage(AgendaViewModel viewModel, UserPreferencesService preferences)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        this.preferences = preferences;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        preferences.CityChanged += OnCityChanged;
        preferences.FavoritesChanged += OnFavoritesChanged;
        viewModel.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        preferences.CityChanged -= OnCityChanged;
        preferences.FavoritesChanged -= OnFavoritesChanged;
    }

    private void OnCityChanged(object? sender, EventArgs e) =>
        viewModel.LoadCommand.Execute(null);

    private void OnFavoritesChanged(object? sender, EventArgs e) =>
        viewModel.LoadCommand.Execute(null);
}
