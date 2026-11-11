using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class FavoritesPage : ContentPage
{
    private readonly FavoritesViewModel viewModel;

    public FavoritesPage(FavoritesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.LoadCommand.Execute(null);
    }
}
