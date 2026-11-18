using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.ViewModels;

public partial class ChatViewModel(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    ChatService chat,
    IDispatcher dispatcher,
    ILogger<ChatViewModel> logger) : BaseViewModel(dbProvider, preferences, dispatcher, logger)
{
    private CancellationTokenSource? activeTurn;

    public ObservableCollection<ChatItem> Messages => chat.Messages;
    public IReadOnlyList<string> Suggestions => chat.Suggestions;

    [ObservableProperty]
    public partial string Prompt { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? UnavailableReason { get; set; }

    protected override async Task OnLoadAsync() =>
        UnavailableReason = await chat.GetUnavailableReasonAsync();

    [RelayCommand]
    private Task AskSuggestionAsync(string suggestion)
    {
        Prompt = suggestion;
        return SendAsync();
    }

    [RelayCommand]
    private void ClearChat()
    {
        activeTurn?.Cancel();
        Messages.Clear();
        Prompt = "";
        IsBusy = false;
    }

    [RelayCommand]
    private Task OpenSessionAsync(Session? session) =>
        session is null ? Task.CompletedTask : Shell.Current.GoToAsync($"session?id={session.Id}");

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = Prompt.Trim();
        if (string.IsNullOrEmpty(text) || IsBusy)
            return;

        IsBusy = true;
        using var turn = new CancellationTokenSource();
        activeTurn = turn;
        ChatItem? reply = null;
        List<ChatItem> history = [];

        try
        {
            UnavailableReason = await chat.GetUnavailableReasonAsync(turn.Token);
            if (!string.IsNullOrEmpty(UnavailableReason))
                return;

            Prompt = "";
            Messages.Add(new ChatItem(isUser: true, text));
            history = Messages.Where(message => !message.IsError).ToList();
            reply = new ChatItem(isUser: false);
            Messages.Add(reply);

            await foreach (var chunk in chat.SendAsync(history, reply, turn.Token))
                reply.Text += chunk;
        }
        catch (OperationCanceledException) when (turn.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (reply is not null)
            {
                reply.IsError = true;
                if (history.Count > 0)
                    history[^1].IsError = true;
                reply.Progress = "";
                reply.Text = string.IsNullOrWhiteSpace(reply.Text) ? ex.Message : $"{reply.Text}\n\n{ex.Message}";
            }
        }
        finally
        {
            if (reply is not null)
                reply.Progress = "";
            if (ReferenceEquals(activeTurn, turn))
            {
                activeTurn = null;
                IsBusy = false;
            }
        }
    }
}
