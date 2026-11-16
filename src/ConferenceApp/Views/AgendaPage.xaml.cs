using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class AgendaPage : BasePage
{
    public AgendaPage(AgendaViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}
