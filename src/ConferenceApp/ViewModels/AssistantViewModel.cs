using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.ViewModels;

public sealed partial class AssistantViewModel(AssistantService assistant, DayPlannerService planner, FavoritesService favorites, FavoriteApproval approval, ILogger<AssistantViewModel> logger) : ObservableObject
{
    public FavoriteApproval Approval { get; } = approval;
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
        Approval.BeginRequest();
        var request = new CancellationTokenSource();
        currentRequest = request;
        IsBusy = true;
        var requestCityId = assistant.SelectedCityId;
        Messages.Add(new AssistantMessage("You", question));
        var answer = new AssistantMessage("Assistant", "Thinking...");
        Messages.Add(answer);
        var hasResponseText = false;
        void ApprovalChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(FavoriteApproval.IsPending))
                answer.Text = Approval.IsPending ? "Waiting for your approval..." : Approval.GetResults() ?? "Thinking...";
        }
        Approval.PropertyChanged += ApprovalChanged;
        try
        {
            var instructions = await assistant.BuildInstructionsAsync(IsDebug && UseSimulatedTime ? SimulatedTime : null);
            var prompt = new List<ChatMessage> { new(ChatRole.System, instructions) };
            prompt.AddRange(history.TakeLast(4));
            prompt.Add(new ChatMessage(ChatRole.User, question));
            await foreach (var update in assistant.StreamAsync(prompt, request.Token))
            {
                request.Token.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(update.Text))
                {
                    if (!hasResponseText)
                        answer.Text = "";
                    hasResponseText = true;
                    answer.Text = Approval.GetResults() ?? (answer.Text + update.Text);
                }
            }
            request.Token.ThrowIfCancellationRequested();
            answer.Text = Approval.GetResults() ?? answer.Text;
            if (!hasResponseText && Approval.GetResults() is null)
                throw new InvalidOperationException("The model returned an empty response.");
            var mentioned = await assistant.FindMentionedSessionsAsync(answer.Text, requestCityId);
            request.Token.ThrowIfCancellationRequested();
            foreach (var session in mentioned)
                answer.Sessions.Add(new AssistantSessionLink(session.Id, session.Title));
            history.Add(new ChatMessage(ChatRole.User, question));
            history.Add(new ChatMessage(ChatRole.Assistant, answer.Text.Length <= 600 ? answer.Text : answer.Text[..600]));
            if (history.Count > 4)
                history.RemoveRange(0, history.Count - 4);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            if (Messages.Contains(answer))
                answer.Text = Approval.GetResults() is { } result
                    ? result + "\n\nResponse cancelled."
                    : "Response cancelled; no pending favourite change was made.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "On-device assistant request failed.");
            answer.Text = Approval.GetResults() is { } result
                ? result + "\n\nThe assistant couldn't finish its response."
                : "Sorry, I couldn't answer on this device. Please try again.";
        }
        finally
        {
            Approval.PropertyChanged -= ApprovalChanged;
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
    private async Task PlanDayAsync()
    {
        var question = Input.Trim();
        if (!IsAvailable || IsBusy || question.Length == 0)
            return;
        Input = "";
        var request = new CancellationTokenSource();
        currentRequest = request;
        IsBusy = true;
        var cityId = assistant.SelectedCityId;
        Messages.Add(new AssistantMessage("You", question));
        var answer = new AssistantMessage("Day plan", "Finding a non-overlapping plan on your device...");
        Messages.Add(answer);
        try
        {
            var plan = await planner.PlanAsync(question, request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (plan.Items.Count == 0)
            {
                answer.Text = "No matching talks fit those preferences. Try different interests or a later departure time.";
                return;
            }
            var departure = plan.Preferences.LeaveBy.Length == 0 ? "No departure time specified" : $"Leaving by {plan.Preferences.LeaveBy}";
            answer.Text = $"Interests: {string.Join(", ", plan.Preferences.Interests)} · {departure}";
            foreach (var item in plan.Items)
                answer.Sessions.Add(new AssistantSessionLink(item.Session.Id, item.Session.Title,
                    $"{item.Session.Start:HH:mm}–{item.Session.End:HH:mm} · {item.Session.Track}", item.Reason));
            var approved = await Approval.RequestAsync($"Add all {plan.Items.Count} talks in this plan to favourites?", request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (!approved)
            {
                answer.Text += "\n\nNot added to favourites.";
                return;
            }
            if (assistant.SelectedCityId != cityId || plan.CityId != cityId)
                throw new InvalidOperationException("The selected city changed before approval.");
            favorites.AddAll(plan.Items.Select(item => item.Session.Id));
            answer.Text += "\n\nAdded the plan to favourites.";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            if (Messages.Contains(answer))
                answer.Text += "\n\nPlanning cancelled; favourites were not changed.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to plan the conference day.");
            answer.Text += "\n\nCouldn't finish the plan; favourites were not changed. Please try again.";
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
    private Task OpenSessionAsync(AssistantSessionLink session) =>
        Shell.Current.GoToAsync($"session?id={Uri.EscapeDataString(session.Id)}");

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
    public ObservableCollection<AssistantSessionLink> Sessions { get; } = [];
}

public sealed record AssistantSessionLink(string Id, string Title, string Metadata = "", string Reason = "")
{
    public string AutomationId => $"assistant-session-{Id}";
    public bool HasMetadata => Metadata.Length > 0;
    public bool HasReason => Reason.Length > 0;
}
