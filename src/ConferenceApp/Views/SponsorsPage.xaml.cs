using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SponsorsPage : ContentPage
{
    private readonly SponsorsViewModel viewModel;

    public SponsorsPage(SponsorsViewModel viewModel)
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
