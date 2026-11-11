using CommunityToolkit.Mvvm.ComponentModel;

namespace ConferenceApp.Models;

public partial class ChatItem(bool isUser, string text = "") : ObservableObject
{
    public bool IsUser { get; } = isUser;

    [ObservableProperty]
    public partial string Text { get; set; } = text;
}
