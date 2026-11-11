using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class ExplorePage : ContentPage
{
    private readonly ExploreViewModel viewModel;

    public ExplorePage(ExploreViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }
}
