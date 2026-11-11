using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ConferenceApp.ViewModels;

public partial class AssistantViewModel(AssistantService assistant, DayPlannerService planner, CloudRoutingService routing, ConferenceService conference, FavoriteApproval approval, ILogger<AssistantViewModel> logger) : ObservableObject
{
    public FavoriteApproval Approval { get; } = approval;
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
    [ObservableProperty] public partial bool CloudEnabled { get; set; }
    [ObservableProperty] public partial bool ShowCloudSettings { get; set; } = true;
    [ObservableProperty] public partial string CloudKey { get; set; } = "";
    [ObservableProperty] public partial string CloudEndpoint { get; set; } = CloudRoutingService.DefaultEndpoint;
    [ObservableProperty] public partial string CloudModel { get; set; } = CloudRoutingService.DefaultModel;
    public bool CanSend => IsAvailable && !IsBusy && !string.IsNullOrWhiteSpace(Input);
    public bool CanCancel => IsBusy;

    partial void OnInputChanged(string value) => OnPropertyChanged(nameof(CanSend));
    partial void OnIsAvailableChanged(bool value) => OnPropertyChanged(nameof(CanSend));
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanSend)); OnPropertyChanged(nameof(CanCancel)); }
    partial void OnCloudEnabledChanged(bool value)
    {
        if (!value) CloudKey = "";
        else ShowCloudSettings = true;
    }
    public string CloudSettingsButtonText => ShowCloudSettings ? "Hide cloud settings" : "Show cloud settings";
    partial void OnShowCloudSettingsChanged(bool value) => OnPropertyChanged(nameof(CloudSettingsButtonText));

    [RelayCommand]
    private void ToggleCloudSettings() => ShowCloudSettings = !ShowCloudSettings;

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
        var route = "Apple Intelligence (on device)";
        var useCloud = CloudEnabled && !string.IsNullOrWhiteSpace(CloudKey);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(source.Token);
        var toolGuard = new ToolErrorGuard(requestCancellation);
        try
        {
            var cityId = conference.CityId;
            using var toolSession = await assistant.CreateToolSessionAsync(cityId);
            if (useCloud && toolSession.Client is null)
                throw new InvalidOperationException("Cloud routing requires on-device tool support so both models use the same tools.");
            var (instructions, conversationKey) = await assistant.BuildInstructionsAsync(UseEventTime ? EventTime : null, toolSession.Client is not null);
            if (conference.CityId != cityId)
                throw new InvalidOperationException("The selected city changed while preparing the answer.");
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
            // Favourite tools perform their own shared approval before changing state.
            var options = toolSession.Client is null ? null : new ChatOptions
            {
                Tools = [.. ConferenceToolContext.Default.Tools.Select(toolGuard.Wrap)],
                AllowMultipleToolCalls = false
            };
            using var routed = useCloud ? routing.Create(toolSession.Client!, toolSession.Services, CloudKey.Trim(), CloudEndpoint.Trim(), CloudModel.Trim(),
                name => { route = name; reply.Author = name; }) : null;
            if (!useCloud) reply.Author = route;
            await foreach (var update in (routed?.Client ?? toolSession.Client ?? client).GetStreamingResponseAsync(request, options, requestCancellation.Token))
            {
                if (version != conversationVersion) return;
                reply.Text += update.Text;
            }
            if (toolGuard.Failed)
                throw new InvalidOperationException("A tool failed.");
            if (string.IsNullOrWhiteSpace(reply.Text))
                throw new InvalidOperationException("The model returned an empty response.");
            if (toolSession.Client is not null)
            {
                var data = await conference.GetConferenceAsync();
                foreach (var session in toolSession.Tools.ReturnedSessions)
                    reply.Sessions.Add(new SessionItem(session, data, conference));
            }
            history.Add(new ChatMessage(ChatRole.User, question));
            history.Add(new ChatMessage(ChatRole.Assistant, reply.Text));
        }
        catch (Exception) when (toolGuard.Failed && !source.IsCancellationRequested)
        {
            logger.LogWarning("A conference tool failed; the request was stopped.");
            if (version == conversationVersion)
                reply.Text = "A tool couldn't complete the request. The assistant stopped without retrying.";
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            if (version == conversationVersion)
                reply.Text = string.IsNullOrEmpty(reply.Text) ? "Response cancelled." : $"{reply.Text}\n\nResponse cancelled.";
        }
        catch (Exception ex)
        {
            if (useCloud)
                logger.LogWarning("Cloud chat failed; the request was not retried.");
            else
                logger.LogWarning(ex, "Assistant chat failed.");
            if (version == conversationVersion)
                reply.Text = route.StartsWith("GitHub Models:", StringComparison.Ordinal)
                    ? "The cloud model couldn't answer. Check the endpoint, model, key, and connection. Nothing was retried."
                    : "The assistant couldn't answer. Check the on-device model and cloud settings, then try again.";
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
    private async Task PlanDayAsync()
    {
        if (!CanSend)
            return;

        var request = Input.Trim();
        Input = "";
        var version = conversationVersion;
        using var source = new CancellationTokenSource();
        cancellation = source;
        IsBusy = true;
        Messages.Add(new AssistantMessage("You", request));
        var reply = new AssistantMessage("Plan my day", "Planning your day on this device...");
        Messages.Add(reply);
        try
        {
            var cityId = conference.CityId;
            var plan = await planner.PlanAsync(request, cityId, source.Token);
            source.Token.ThrowIfCancellationRequested();
            if (version != conversationVersion)
                return;
            if (conference.CityId != cityId)
                throw new InvalidOperationException("The selected city changed while planning.");
            var data = await conference.GetConferenceAsync();
            foreach (var talk in plan.Talks)
                reply.Sessions.Add(new SessionItem(talk.Session, data, conference, matchText: talk.Reason));
            if (plan.Talks.Count == 0)
            {
                reply.Text = "No matching talks fit those preferences and the available time. Try different interests or a later departure.";
                return;
            }
            var departure = plan.LeaveBy is null ? "no departure time specified" : $"leaving by {plan.LeaveBy}";
            reply.Text = $"Your suggested day in {data.Cities.First(c => c.Id == cityId).Name}, for {string.Join(", ", plan.Interests)}; {departure}. {plan.Warning}".Trim();
            var approved = await Approval.RequestAsync($"Add all {plan.Talks.Count} suggested talks to favourites?", source.Token);
            source.Token.ThrowIfCancellationRequested();
            if (version != conversationVersion)
                return;
            if (!approved)
            {
                reply.Text += "\n\nNo favourites were changed.";
                return;
            }
            if (conference.CityId != cityId)
                throw new InvalidOperationException("The selected city changed before approval.");
            foreach (var talk in plan.Talks)
                conference.SetFavorite(talk.Session.Id, true);
            reply.Text += "\n\nAdded all suggested talks to favourites.";
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            if (version == conversationVersion)
                reply.Text += "\n\nPlanning cancelled; no favourites were changed.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Planning the day failed.");
            if (version == conversationVersion)
                reply.Text = "Couldn't plan your day on this device. Please check your request and try again.";
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
    [ObservableProperty] public partial string Author { get; set; } = author;
    public ObservableCollection<SessionItem> Sessions { get; } = [];
    [ObservableProperty] public partial string Text { get; set; } = text;
    public bool IsVisible => Author != "Assistant" || !string.IsNullOrEmpty(Text);
    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(IsVisible));
    partial void OnAuthorChanged(string value) => OnPropertyChanged(nameof(IsVisible));
}
