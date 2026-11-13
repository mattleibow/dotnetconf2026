using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;

namespace ConferenceApp.Models;

public partial class ChatItem(bool isUser, string text = "") : ObservableObject
{
    public bool IsUser { get; } = isUser;
    public ObservableCollection<Session> Sessions { get; } = [];
    public IReadOnlyList<ChatMessage> ResponseMessages { get; set; } = [];

    [ObservableProperty]
    public partial string Text { get; set; } = text;

    [ObservableProperty]
    public partial string Progress { get; set; } = "";
}
