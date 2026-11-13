using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SessionPage : BasePage
{
    public SessionPage(SessionViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}
