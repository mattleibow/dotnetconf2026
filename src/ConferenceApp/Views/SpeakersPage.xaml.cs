using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SpeakersPage : BasePage
{
    public SpeakersPage(SpeakersViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}
