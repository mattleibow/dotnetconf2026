using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SponsorPage : ContentPage
{
    public SponsorPage(SponsorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
