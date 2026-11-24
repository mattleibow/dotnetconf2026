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
    public ObservableCollection<Session> Sessions => chat.Sessions;
    public IReadOnlyList<string> Suggestions => chat.Suggestions;

    [ObservableProperty]
    public partial string Prompt { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    protected override Task OnLoadAsync() => Task.CompletedTask;

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
        chat.Clear();
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

        try
        {
            Prompt = "";
            await chat.SendAsync(text, turn.Token);
        }
        catch (OperationCanceledException) when (turn.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(activeTurn, turn))
            {
                activeTurn = null;
                IsBusy = false;
            }
        }
    }
}
