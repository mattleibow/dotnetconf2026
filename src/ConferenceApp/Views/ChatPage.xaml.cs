using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class ChatPage : BasePage
{
    public ChatPage(ChatViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}
