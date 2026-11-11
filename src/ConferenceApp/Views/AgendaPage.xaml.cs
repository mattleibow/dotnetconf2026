using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class AgendaPage : ContentPage
{
    private readonly AgendaViewModel viewModel;

    public AgendaPage(AgendaViewModel viewModel)
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
