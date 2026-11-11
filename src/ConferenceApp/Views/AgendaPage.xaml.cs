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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        viewModel.IsWide = width >= 800;
    }
}
