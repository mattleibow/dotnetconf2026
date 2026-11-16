using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public abstract class BasePage : ContentPage
{
    protected BasePage(BaseViewModel viewModel)
    {
        BindingContext = ViewModel = viewModel;
    }

    protected BaseViewModel ViewModel { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ViewModel.LoadCommand.Execute(null);
    }
}
