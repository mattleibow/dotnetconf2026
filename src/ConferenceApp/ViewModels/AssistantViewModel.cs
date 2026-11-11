using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.ViewModels;

public partial class AssistantViewModel(AssistantService assistant, ILogger<AssistantViewModel> logger) : ObservableObject
{
    private CancellationTokenSource? cancellation;
    private int conversationVersion;
    private readonly List<ChatMessage> history = [];
    private string? previousConversationKey;
    private TimeSpan? previousEventTime;
    public ObservableCollection<AssistantMessage> Messages { get; } = [];
    [ObservableProperty] public partial string Input { get; set; } = "";
    [ObservableProperty] public partial string Notice { get; set; } = "Checking on-device model...";
    [ObservableProperty] public partial bool IsAvailable { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool ShowDebugClock { get; set; }
    [ObservableProperty] public partial bool UseEventTime { get; set; }
    [ObservableProperty] public partial TimeSpan EventTime { get; set; } = new(13, 0, 0);
    public bool CanSend => IsAvailable && !IsBusy && !string.IsNullOrWhiteSpace(Input);
    public bool CanCancel => IsBusy;

    partial void OnInputChanged(string value) => OnPropertyChanged(nameof(CanSend));
    partial void OnIsAvailableChanged(bool value) => OnPropertyChanged(nameof(CanSend));
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanSend)); OnPropertyChanged(nameof(CanCancel)); }

    public async Task CheckAvailabilityAsync()
    {
        try
        {
            IsAvailable = await assistant.IsAvailableAsync() && assistant.Client is not null;
            Notice = IsAvailable ? "On-device answers can make mistakes. Check the agenda for details." :
                "On-device chat is unavailable. On a supported Apple device, enable Apple Intelligence and check that its model is ready.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to check on-device chat availability.");
            IsAvailable = false;
            Notice = "Couldn't check the on-device model. Please try again later.";
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (!CanSend || assistant.Client is not { } client)
            return;

        var question = Input.Trim();
        Input = "";
        var version = conversationVersion;
        using var source = new CancellationTokenSource();
        cancellation = source;
        IsBusy = true;
        Messages.Add(new AssistantMessage("You", question));
        var reply = new AssistantMessage("Assistant", "");
        Messages.Add(reply);
        try
        {
            var (instructions, conversationKey) = await assistant.BuildInstructionsAsync(UseEventTime ? EventTime : null);
            source.Token.ThrowIfCancellationRequested();
            var eventTime = UseEventTime ? EventTime : (TimeSpan?)null;
            if (previousConversationKey != conversationKey || previousEventTime != eventTime)
                history.Clear();
            previousConversationKey = conversationKey;
            previousEventTime = eventTime;
            var request = new List<ChatMessage> { new(ChatRole.System, instructions) };
            if (history.Count >= 2)
                request.AddRange(history.TakeLast(2));
            request.Add(new ChatMessage(ChatRole.User, question));
            await foreach (var update in client.GetStreamingResponseAsync(request, cancellationToken: source.Token))
            {
                if (version != conversationVersion) return;
                reply.Text += update.Text;
            }
            if (string.IsNullOrWhiteSpace(reply.Text))
                throw new InvalidOperationException("The on-device model returned an empty response.");
            history.Add(new ChatMessage(ChatRole.User, question));
            history.Add(new ChatMessage(ChatRole.Assistant, reply.Text));
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            if (version == conversationVersion)
                reply.Text = string.IsNullOrEmpty(reply.Text) ? "Response cancelled." : $"{reply.Text}\n\nResponse cancelled.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "On-device chat failed.");
            if (version == conversationVersion)
                reply.Text = "Sorry, the on-device model couldn't answer. Please try again.";
        }
        finally
        {
            if (version == conversationVersion)
            {
                cancellation = null;
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private void Cancel() => cancellation?.Cancel();

    [RelayCommand]
    private void NewChat()
    {
        cancellation?.Cancel();
        conversationVersion++;
        cancellation = null;
        IsBusy = false;
        history.Clear();
        previousConversationKey = null;
        previousEventTime = null;
        Messages.Clear();
        Input = "";
    }

    [RelayCommand]
    private void ToggleDebugClock() => ShowDebugClock = !ShowDebugClock;
}

public partial class AssistantMessage(string author, string text) : ObservableObject
{
    public string Author { get; } = author;
    [ObservableProperty] public partial string Text { get; set; } = text;
}
