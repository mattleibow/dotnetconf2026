using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SpeakerPage : BasePage
{
    public SpeakerPage(SpeakerViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}
