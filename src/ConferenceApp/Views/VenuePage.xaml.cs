using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class VenuePage : ContentPage
{
    private readonly VenueViewModel viewModel;

    public VenuePage(VenueViewModel viewModel)
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
