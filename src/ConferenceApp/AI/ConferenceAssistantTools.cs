using System.ComponentModel;
using ConferenceApp.Models;
using ConferenceApp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceApp.AI;

[AIToolSource(typeof(ConferenceAssistantTools))]
public partial class ConferenceToolContext : AIToolContext;

public sealed class ConferenceAssistantTools(
    ConferenceDatabase database,
    UserPreferencesService preferences,
    SearchService search,
    SessionEnrichmentService enrichment,
    ILogger<ConferenceAssistantTools> logger)
{
    // The active chat bubble for the current turn (used for progress text and approval cards).
    public ChatItem? CurrentReply { get; set; }

    [ExportAIFunction("search_sessions")]
    [Description("Search sessions by topic, title, or speaker in the current city. Returns summaries; call get_session for the full abstract.")]
    public async Task<string> SearchSessionsAsync(
        [Description("Topic, title, or speaker to search for.")] string query,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        logger.LogInformation("[AI Tool] search_sessions(query: '{Query}') in {CityId}", query, city.Id);
        await SetProgressAsync($"Searching sessions for '{query}'...");

        var sessions = await search.SearchSessionsAsync(city.Id, query);
        return await FormatSessionsAsync(city, sessions.Take(5));
    }

    [ExportAIFunction("get_session")]
    [Description("Read the full abstract and schedule details for a specific session in the current city.")]
    public async Task<string> GetSessionAsync(
        [Description("Session ID or title.")] string session,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        var match = await FindSessionAsync(city.Id, session);

        logger.LogInformation("[AI Tool] get_session('{SessionId}')", match.Id);
        await SetProgressAsync($"Reading abstract for '{match.Title}'...");

        var summary = await FormatSessionsAsync(city, [match]);
        var abstractText = await database.GetTextAsync(match.Content);
        return $"{summary}\nAbstract: {abstractText}";
    }

    [ExportAIFunction("get_speaker")]
    [Description("Read a speaker's tagline and full biography in the current city.")]
    public async Task<string> GetSpeakerAsync(
        [Description("Speaker ID or name.")] string speaker,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        var speakers = await database.GetSpeakersAsync(city.Id);

        var match = speakers.FirstOrDefault(s =>
            s.Id.Equals(speaker, StringComparison.OrdinalIgnoreCase) ||
            s.Name.Contains(speaker, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No speaker matching '{speaker}' in {city.Name}.", nameof(speaker));

        logger.LogInformation("[AI Tool] get_speaker('{SpeakerId}')", match.Id);
        await SetProgressAsync($"Reading biography for {match.Name}...");

        var bio = await database.GetTextAsync(match.Content);
        return $"Name: {match.Name} | {match.Tagline}\nBiography: {bio}";
    }

    [ExportAIFunction("list_sessions")]
    [Description("List sessions in the current city between two 24-hour daytime times (for example 14:00 to 16:00).")]
    public async Task<string> ListSessionsAsync(
        [Description("Start time in 24-hour HH:mm format (e.g. 13:00).")] TimeOnly start,
        [Description("End time in 24-hour HH:mm format (e.g. 16:00).")] TimeOnly end,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        logger.LogInformation("[AI Tool] list_sessions(start: {Start}, end: {End}) in {CityId}", start, end, city.Id);
        await SetProgressAsync($"Listing sessions between {start:HH:mm} and {end:HH:mm}...");

        var sessions = await database.GetSessionsAsync(city.Id);
        var matching = sessions.Where(s =>
            TimeOnly.FromDateTime(s.Start.DateTime) < end &&
            TimeOnly.FromDateTime(s.End.DateTime) > start);

        return await FormatSessionsAsync(city, matching);
    }

    [ExportAIFunction("get_favorites")]
    [Description("List the user's saved favourite sessions in the current city.")]
    public async Task<string> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        logger.LogInformation("[AI Tool] get_favorites() in {CityId}", city.Id);
        await SetProgressAsync("Checking your saved favourites...");

        var sessions = await database.GetSessionsAsync(city.Id);
        return await FormatSessionsAsync(city, sessions.Where(s => preferences.IsFavorite(s.Id)));
    }

    [ExportAIFunction("add_favorite", ApprovalRequired = true)]
    [Description("Add a session to favourites after the user approves an inline confirmation card.")]
    public Task<string> AddFavoriteAsync(
        [Description("Session ID or title.")] string session,
        CancellationToken cancellationToken = default) =>
        ChangeFavoriteAsync(session, add: true, cancellationToken);

    [ExportAIFunction("remove_favorite", ApprovalRequired = true)]
    [Description("Remove a session from favourites after the user approves an inline confirmation card.")]
    public Task<string> RemoveFavoriteAsync(
        [Description("Session ID or title.")] string session,
        CancellationToken cancellationToken = default) =>
        ChangeFavoriteAsync(session, add: false, cancellationToken);

    private async Task<string> ChangeFavoriteAsync(string session, bool add, CancellationToken cancellationToken)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        var match = await FindSessionAsync(city.Id, session);
        var action = add ? "add_favorite" : "remove_favorite";

        logger.LogInformation("[AI Tool] {Action}('{SessionId}') -> waiting for user approval...", action, match.Id);
        await SetProgressAsync($"Waiting for your approval to {(add ? "add" : "remove")} '{match.Title}'...");

        // Show an inline Approve / Deny card in the chat bubble and wait for the user's decision.
        var approval = new ChatApproval(match, add);
        await MainThread.InvokeOnMainThreadAsync(() => CurrentReply?.Approvals.Add(approval));

        if (!await approval.WaitAsync(cancellationToken))
        {
            logger.LogInformation("[AI Tool] {Action}('{SessionId}') denied by user.", action, match.Id);
            return $"Denied by the user. No favourite was changed for {match.Title}. Do not retry.";
        }

        await MainThread.InvokeOnMainThreadAsync(() => preferences.SetFavorite(match.Id, add));
        logger.LogInformation("[AI Tool] {Action}('{SessionId}') approved and saved.", action, match.Id);

        return add
            ? $"Added '{match.Title}' to favourites."
            : $"Removed '{match.Title}' from favourites.";
    }

    // Attaches tappable session cards under the assistant reply for any talk mentioned in the response.
    public async Task AddReferencedSessionsAsync(string text)
    {
        if (CurrentReply is not { } reply)
            return;

        var city = await preferences.GetSelectedCityAsync(database);
        var conference = await database.GetConferenceAsync();
        var sessions = await database.GetSessionsAsync(city.Id);

        var referenced = sessions
            .Where(s => s.IsTalk && (
                text.Contains(s.Title, StringComparison.OrdinalIgnoreCase) ||
                text.Contains(s.Id, StringComparison.OrdinalIgnoreCase) ||
                reply.Approvals.Any(a => a.Sessions.Any(picked => picked.Id == s.Id))))
            .ToArray();

        if (referenced.Length == 0)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            foreach (var session in referenced)
            {
                if (reply.Sessions.Any(card => card.Session.Id == session.Id))
                    continue;

                var speakers = string.Join(", ", session.SpeakerIds.Select(id =>
                    conference.Speakers.First(s => s.Id == id).Name));
                var meta = $"{city.Name} · {session.Time} · {session.Track ?? "All attendees"}";

                reply.Sessions.Add(new ChatSessionCard(session, meta, speakers));
            }
        });
    }

    internal async Task<string> FormatSessionsAsync(City city, IEnumerable<Session> sessions)
    {
        var matches = sessions.ToArray();
        if (matches.Length == 0)
            return $"No matching sessions in {city.Name}.";

        var conference = await database.GetConferenceAsync();
        var lines = new List<string>(matches.Length);

        foreach (var session in matches)
        {
            var speakers = string.Join(", ", session.SpeakerIds.Select(id =>
                conference.Speakers.First(s => s.Id == id).Name));
            var enriched = enrichment.GetSession(session.Id);
            var topic = enriched.HasTopic ? $" [{enriched.Topic}]" : "";
            var summary = !string.IsNullOrWhiteSpace(enriched.Summary) ? $" — {enriched.Summary}" : "";

            lines.Add($"{session.Time} | {session.Track ?? "All attendees"}{topic} | {session.Title} | {speakers}{summary}");
        }

        return string.Join('\n', lines);
    }

    private async Task<Session> FindSessionAsync(string cityId, string value)
    {
        var sessions = await database.GetSessionsAsync(cityId);

        return sessions.FirstOrDefault(s =>
            s.Id.Equals(value, StringComparison.OrdinalIgnoreCase) ||
            s.Title.Contains(value, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No session matching '{value}'.", nameof(value));
    }

    private Task SetProgressAsync(string message) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (CurrentReply is not null)
                CurrentReply.Progress = message;
        });
}
