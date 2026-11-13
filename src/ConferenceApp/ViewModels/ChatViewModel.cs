using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Data;
using ConferenceApp.Services;

namespace ConferenceApp.ViewModels;

public partial class ChatViewModel : ObservableObject, IRecipient<CityChangedMessage>
{
    private readonly ChatService chat;
    private readonly UserPreferencesService preferences;
    private CancellationTokenSource? activeTurn;

    public ChatViewModel(ChatService chat, UserPreferencesService preferences)
    {
        this.chat = chat;
        this.preferences = preferences;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public ObservableCollection<ChatItem> Messages => chat.Messages;
    public IReadOnlyList<string> Suggestions => chat.Suggestions;

    [ObservableProperty]
    public partial City? SelectedCity { get; set; }

    [ObservableProperty]
    public partial string Prompt { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? UnavailableReason { get; set; }

    public void Receive(CityChangedMessage message) =>
        _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        SelectedCity = await preferences.GetSelectedCityAsync();
        UnavailableReason = await chat.GetUnavailableReasonAsync();
    }

    [RelayCommand]
    private Task PickCityAsync() =>
        preferences.PickCityAsync();

    [RelayCommand]
    private Task AskSuggestionAsync(string suggestion)
    {
        Prompt = suggestion;
        return SendAsync();
    }

    [RelayCommand]
    private Task PlanMyDayAsync()
    {
        var constraints = Prompt.Trim();
        Prompt = string.IsNullOrEmpty(constraints) || ChatService.IsDayPlanRequest(constraints)
            ? (string.IsNullOrEmpty(constraints) ? "Plan my day" : constraints)
            : $"Plan my day: {constraints}";
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

        UnavailableReason = await chat.GetUnavailableReasonAsync();
        if (!string.IsNullOrEmpty(UnavailableReason))
            return;

        using var turn = new CancellationTokenSource();
        activeTurn = turn;
        Prompt = "";
        Messages.Add(new ChatItem(isUser: true, text));
        var history = Messages.ToList();
        var reply = new ChatItem(isUser: false);
        Messages.Add(reply);

        IsBusy = true;
        try
        {
            await foreach (var chunk in chat.SendAsync(history, reply, turn.Token))
                reply.Text += chunk;
        }
        catch (OperationCanceledException) when (turn.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            reply.Progress = "";
            reply.Text = string.IsNullOrWhiteSpace(reply.Text) ? ex.Message : $"{reply.Text}\n\n{ex.Message}";
        }
        finally
        {
            reply.Progress = "";
            if (ReferenceEquals(activeTurn, turn))
            {
                activeTurn = null;
                IsBusy = false;
            }
        }
    }
}
