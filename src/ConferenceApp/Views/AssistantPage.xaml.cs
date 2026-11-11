using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class AssistantPage : ContentPage
{
    private readonly AssistantViewModel viewModel;

    public AssistantPage(AssistantViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.CheckAvailabilityAsync();
    }
}
