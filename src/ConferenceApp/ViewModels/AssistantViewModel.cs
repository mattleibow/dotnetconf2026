using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.ViewModels;

public sealed partial class AssistantViewModel(AssistantService assistant, ILogger<AssistantViewModel> logger) : ObservableObject
{
    private CancellationTokenSource? currentRequest;
    private readonly List<ChatMessage> history = [];
    private string? lastCityId;
    [ObservableProperty] public partial string Input { get; set; } = "";
    [ObservableProperty] public partial string Status { get; set; } = "Checking the on-device model...";
    [ObservableProperty] public partial bool IsAvailable { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool UseSimulatedTime { get; set; }
    [ObservableProperty] public partial TimeSpan SimulatedTime { get; set; } = new(13, 0, 0);
    public ObservableCollection<AssistantMessage> Messages { get; } = [];
#if DEBUG
    public bool IsDebug => true;
#else
    public bool IsDebug => false;
#endif

    public async Task CheckAvailabilityAsync()
    {
        try
        {
            if (lastCityId is not null && lastCityId != assistant.SelectedCityId)
                NewChat();
            lastCityId = assistant.SelectedCityId;
            var cityName = await assistant.GetCityLabelAsync();
            var reason = await assistant.GetUnavailableReasonAsync();
            IsAvailable = reason is null;
            Status = reason ?? $"{cityName} agenda · On-device AI can make mistakes; check the agenda for times.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to check the on-device model.");
            IsAvailable = false;
            Status = "Couldn't check the on-device model. Please try again later.";
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var question = Input.Trim();
        if (!IsAvailable || IsBusy || question.Length == 0)
            return;
        Input = "";
        var request = new CancellationTokenSource();
        currentRequest = request;
        IsBusy = true;
        Messages.Add(new AssistantMessage("You", question));
        var answer = new AssistantMessage("Assistant", "");
        Messages.Add(answer);
        try
        {
            var instructions = await assistant.BuildInstructionsAsync(IsDebug && UseSimulatedTime ? SimulatedTime : null);
            var prompt = new List<ChatMessage> { new(ChatRole.System, instructions) };
            prompt.AddRange(history.TakeLast(4));
            prompt.Add(new ChatMessage(ChatRole.User, question));
            await foreach (var update in assistant.StreamAsync(prompt, request.Token))
            {
                request.Token.ThrowIfCancellationRequested();
                answer.Text += update.Text;
            }
            request.Token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(answer.Text))
                throw new InvalidOperationException("The model returned an empty response.");
            history.Add(new ChatMessage(ChatRole.User, question));
            history.Add(new ChatMessage(ChatRole.Assistant, answer.Text.Length <= 600 ? answer.Text : answer.Text[..600]));
            if (history.Count > 4)
                history.RemoveRange(0, history.Count - 4);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            if (Messages.Contains(answer))
                answer.Text = string.IsNullOrWhiteSpace(answer.Text) ? "Response cancelled." : answer.Text + "\n\nResponse cancelled.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "On-device assistant request failed.");
            answer.Text = "Sorry, I couldn't answer on this device. Please try again.";
        }
        finally
        {
            if (currentRequest == request)
            {
                currentRequest = null;
                IsBusy = false;
            }
            request.Dispose();
        }
    }

    [RelayCommand]
    private void Cancel() => currentRequest?.Cancel();

    [RelayCommand]
    private void NewChat()
    {
        currentRequest?.Cancel();
        history.Clear();
        Messages.Clear();
        Input = "";
    }
}

public sealed partial class AssistantMessage(string role, string text) : ObservableObject
{
    public string Role { get; } = role;
    [ObservableProperty] public partial string Text { get; set; } = text;
}
