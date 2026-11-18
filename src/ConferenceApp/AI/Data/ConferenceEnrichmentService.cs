using System.Diagnostics;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Essentials.AI;

namespace ConferenceApp.AI;

public sealed class ConferenceEnrichmentService(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IChatClient chatClient,
    ILogger<ConferenceEnrichmentService> logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    private static readonly string[] AllowedTopics =
    [
        "AI", "Agents", "Cloud", "Data", "Mobile", "Web",
        "IoT", "Architecture", "Security", "DevOps", "Gaming"
    ];

    private const string SessionPrompt =
        """
        Using ONLY the supplied session title and abstract, produce SessionInsights:
        - Summary: a useful, concise summary in one or two sentences. Do not invent details.
        - Topics: one to three distinct topics chosen only from
          AI, Agents, Cloud, Data, Mobile, Web, IoT, Architecture, Security, DevOps, Gaming.
        If only a title is supplied, use only that title.
        """;

    private const string SpeakerPrompt =
        """
        You are generating an attendee snapshot for a conference speaker badge.
        Using ONLY the speaker's biography and talk descriptions provided, produce a structured SpeakerProfile:
        - Summary: 1 concise sentence (max 22 words) capturing who they are and what they focus on.
        - Expertise: 2 to 3 short technical tags (1-2 words each).
        - AskMeAbout: 1 short, specific topic or question attendees can ask them about in the hallway (max 15 words).
        """;

    private bool attemptTimedOut;
    private int timeouts;
    private int attempts;
    private int fallbacks;
    private int sessionsEnriched;
    private int speakersEnriched;

    protected override async Task ExecuteAsync()
    {
        if (await ChatExtensions.GetModelAvailabilityAsync(StoppingToken, Logger) != ChatModelAvailability.Available)
            return;

        foreach (var session in await GetTalkSessionsAsync())
        {
            StoppingToken.ThrowIfCancellationRequested();
            var insights = await RequestAsync<SessionInsights>(
                SessionPrompt, $"Title: {session.Title}\n\nAbstract: {session.Description}", "session", session.Id);
            await MainThread.InvokeOnMainThreadAsync(() => ApplySessionInsights(session, insights));
            if (!attemptTimedOut && (string.IsNullOrWhiteSpace(session.Summary) || session.Topics.Count == 0))
            {
                fallbacks++;
                Logger.LogWarning(
                    "[AI] Session {SessionId} has missing or invalid insights; trying its title once. HasSummary: {HasSummary}; TopicCount: {TopicCount}.",
                    session.Id, !string.IsNullOrWhiteSpace(session.Summary), session.Topics.Count);
                var fallback = await RequestAsync<SessionInsights>(
                    SessionPrompt, $"Title: {session.Title}", "session-title", session.Id);
                await MainThread.InvokeOnMainThreadAsync(() => ApplySessionInsights(session, fallback));
            }

            Logger.LogInformation("[AI] Session {SessionId} enriched. HasSummary: {HasSummary}; Topics: {Topics}.",
                session.Id, !string.IsNullOrWhiteSpace(session.Summary), string.Join(", ", session.Topics));
            if (!string.IsNullOrWhiteSpace(session.Summary) && session.Topics.Count > 0)
                sessionsEnriched++;
        }

        var db = await DbProvider.GetDbAsync();
        var activeCity = await Preferences.GetSelectedCityAsync();
        foreach (var speaker in db.Speakers.OrderByDescending(s => s.CityIds.Contains(activeCity.Id)))
        {
            StoppingToken.ThrowIfCancellationRequested();
            var talks = db.Sessions
                .Where(s => s.IsTalk && s.SpeakerIds.Contains(speaker.Id))
                .Select(s => $"- {s.Title}: {s.Description}")
                .Distinct();
            var text = $"""
                Speaker: {speaker.Name}
                Tagline: {speaker.Tagline}
                Biography: {speaker.Bio}
                Talks:
                {string.Join("\n", talks)}
                """;

            var profile = await RequestAsync<SpeakerProfile>(SpeakerPrompt, text, "speaker", speaker.Id);
            if (profile is not null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!string.IsNullOrWhiteSpace(profile.Summary))
                        speaker.Summary = profile.Summary.Trim();

                    var expertise = (profile.Expertise ?? [])
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .Select(e => e.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(3)
                        .ToArray();
                    if (expertise.Length > 0)
                        speaker.Expertise = expertise;

                    if (!string.IsNullOrWhiteSpace(profile.AskMeAbout))
                        speaker.AskMeAbout = profile.AskMeAbout.Trim();
                });

                if (!string.IsNullOrWhiteSpace(speaker.Summary))
                    speakersEnriched++;
                Logger.LogInformation(
                    "[AI] Speaker {SpeakerId} enriched. HasSummary: {HasSummary}; ExpertiseCount: {ExpertiseCount}; HasAskMeAbout: {HasAskMeAbout}.",
                    speaker.Id, !string.IsNullOrWhiteSpace(speaker.Summary), speaker.Expertise.Count,
                    !string.IsNullOrWhiteSpace(speaker.AskMeAbout));
                if (string.IsNullOrWhiteSpace(speaker.Summary) || speaker.Expertise.Count == 0 ||
                    string.IsNullOrWhiteSpace(speaker.AskMeAbout))
                    Logger.LogWarning("[AI] Speaker {SpeakerId} has incomplete profile fields.", speaker.Id);
            }

        }

        Logger.LogInformation(
            "[AI] Conference enrichment completed. Sessions: {SessionCount}; Speakers: {SpeakerCount}; Attempts: {AttemptCount}; TitleFallbacks: {FallbackCount}; Timeouts: {TimeoutCount}.",
            sessionsEnriched, speakersEnriched, attempts, fallbacks, timeouts);
    }

    private void ApplySessionInsights(Session session, SessionInsights? insights)
    {
        if (insights is null)
            return;

        // A title-only fallback fills gaps without replacing independently valid fields.
        if (string.IsNullOrWhiteSpace(session.Summary) && !string.IsNullOrWhiteSpace(insights.Summary))
            session.Summary = insights.Summary.Trim();

        if (session.Topics.Count == 0)
        {
            var rejected = (insights.Topics ?? [])
                .Count(topic => !AllowedTopics.Contains(topic?.Trim(), StringComparer.OrdinalIgnoreCase));
            if (rejected > 0)
                Logger.LogWarning("[AI] Dropped {RejectedCount} unknown topics for session {SessionId}.",
                    rejected, session.Id);
            session.Topics = (insights.Topics ?? [])
                .Select(topic => AllowedTopics.FirstOrDefault(allowed =>
                    string.Equals(allowed, topic?.Trim(), StringComparison.OrdinalIgnoreCase)))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();
        }
    }

    private async Task<T?> RequestAsync<T>(string prompt, string text, string kind, string id) where T : class
    {
        StoppingToken.ThrowIfCancellationRequested();
        attempts++;
        attemptTimedOut = false;
        var clock = Stopwatch.StartNew();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(StoppingToken, deadline.Token);
        Task? pending = null;

        try
        {
            Logger.LogInformation("[AI] Enriching {Kind} {ItemId}.", kind, id);
            var request = chatClient.GetResponseAsync<T>(
                new ChatMessage[] { new(ChatRole.System, prompt), new(ChatRole.User, text) },
                new ChatOptions { Temperature = 0, MaxOutputTokens = 512 },
                useJsonSchemaResponseFormat: true,
                cancellationToken: attempt.Token);
            pending = request;
            // Move on at the deadline even if Windows native cancellation has not drained.
            var response = await request.WaitAsync(attempt.Token);
            attempt.Token.ThrowIfCancellationRequested();

            if (response.TryGetResult(out var result) && result is not null)
            {
                Logger.LogInformation("[AI] Structured {Kind} output received for {ItemId} after {ElapsedMs} ms.",
                    kind, id, clock.ElapsedMilliseconds);
                return result;
            }

            Logger.LogWarning("[AI] No valid structured output for {Kind} {ItemId}.", kind, id);
        }
        catch (OperationCanceledException) when (StoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (deadline.IsCancellationRequested)
        {
            attemptTimedOut = true;
            timeouts++;
            Logger.LogWarning(
                "[AI] Enrichment timed out for {Kind} {ItemId} after {ElapsedMs} ms. Moving to the next item without retrying.",
                kind, id, clock.ElapsedMilliseconds);
            if (pending is not null)
                _ = pending.ContinueWith(task => Logger.LogWarning(task.Exception,
                        "[AI] Timed-out {Kind} request for {ItemId} later failed.", kind, id),
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
        catch (Exception exception)
        {
            StoppingToken.ThrowIfCancellationRequested();
            Logger.LogWarning(exception,
                "[AI] Enrichment failed for {Kind} {ItemId} after {ElapsedMs} ms. Source content is unchanged.",
                kind, id, clock.ElapsedMilliseconds);
        }
        return null;
    }
}
