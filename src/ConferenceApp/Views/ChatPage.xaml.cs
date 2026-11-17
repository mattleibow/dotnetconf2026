using System.Collections.Specialized;
using ConferenceApp.Services;
using ConferenceApp.ViewModels;

namespace ConferenceApp.Views;

public partial class ChatPage : BasePage
{
    private readonly ToolbarItem newChatToolbarItem;

    public ChatPage(ChatViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();

        newChatToolbarItem = new ToolbarItem
        {
            AutomationId = "chat-new",
            Text = "New chat",
            Command = viewModel.ClearChatCommand
        };

        viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;
        UpdateToolbarItems(viewModel.Messages.Count > 0);
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateToolbarItems(sender is ICollection<ChatItem> messages && messages.Count > 0);

    private void UpdateToolbarItems(bool hasMessages)
    {
        if (hasMessages && !ToolbarItems.Contains(newChatToolbarItem))
            ToolbarItems.Insert(0, newChatToolbarItem);
        else if (!hasMessages && ToolbarItems.Contains(newChatToolbarItem))
            ToolbarItems.Remove(newChatToolbarItem);
    }
}
