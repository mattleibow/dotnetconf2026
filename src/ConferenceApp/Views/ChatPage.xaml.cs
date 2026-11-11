using ConferenceApp.Services;
using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel viewModel;
    private readonly UserPreferencesService preferences;

    public ChatPage(ChatViewModel viewModel, UserPreferencesService preferences)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        this.preferences = preferences;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        preferences.CityChanged += OnCityChanged;
        viewModel.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        preferences.CityChanged -= OnCityChanged;
    }

    private void OnCityChanged(object? sender, EventArgs e) =>
        viewModel.LoadCommand.Execute(null);
}
