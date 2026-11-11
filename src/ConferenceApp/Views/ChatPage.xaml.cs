using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel viewModel;

    public ChatPage(ChatViewModel viewModel)
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
