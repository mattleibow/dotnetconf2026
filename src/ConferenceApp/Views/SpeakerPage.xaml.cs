using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SpeakerPage : ContentPage
{
    public SpeakerPage(SpeakerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
