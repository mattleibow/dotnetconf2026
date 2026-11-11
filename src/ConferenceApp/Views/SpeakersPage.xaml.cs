using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SpeakersPage : ContentPage
{
    private readonly SpeakersViewModel viewModel;

    public SpeakersPage(SpeakersViewModel viewModel)
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
