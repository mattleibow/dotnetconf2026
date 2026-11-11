using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class SessionPage : ContentPage
{
    public SessionPage(SessionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
