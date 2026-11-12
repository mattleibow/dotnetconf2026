using System.ComponentModel;
using System.Globalization;
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
    ILogger<ConferenceAssistantTools> logger)
{
    private City? city;
    private ChatItem? reply;
    private string? selectedCityId;
    private CancellationToken turnCancellation;

    internal City CurrentCity => city ?? throw new InvalidOperationException("Assistant turn has not started.");
    internal ChatItem CurrentReply => reply ?? throw new InvalidOperationException("Assistant turn has not started.");

    public void BeginTurn(City selectedCity, ChatItem assistantReply, CancellationToken cancellationToken)
    {
        if (reply is not null)
            throw new InvalidOperationException("Assistant tools are scoped to one reply.");
        city = selectedCity;
        reply = assistantReply;
        selectedCityId = preferences.SelectedCityId;
        turnCancellation = cancellationToken;
    }

    [ExportAIFunction("search_sessions")]
    [Description("Find related sessions by topic or meaning in the current city.")]
    public async Task<string> SearchSessionsAsync(
        [Description("Topic, title or speaker to search for.")] string query, CancellationToken cancellationToken = default)
    {
        CheckTurn(cancellationToken);
        logger.LogInformation("[AI Tool] search_sessions(query: '{Query}') in {CityId}", query, city!.Id);
        await SetProgressAsync($"Searching sessions for '{query}'...");
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Enter a topic, title or speaker.", nameof(query));
        var sessions = await search.SearchSessionsAsync(city.Id, query);
        return await DescribeSessionsAsync(sessions, addCards: false, cancellationToken);
    }

    [ExportAIFunction("get_session")]
    [Description("Read a session's full abstract and schedule details in the current city.")]
    public async Task<string> GetSessionAsync(
        [Description("Session ID or title.")] string session, CancellationToken cancellationToken = default)
    {
        var match = await FindSessionAsync(session, cancellationToken);
        logger.LogInformation("[AI Tool] get_session('{SessionId}')", match.Id);
        await SetProgressAsync($"Reading abstract for '{match.Title}'...");
        var description = await database.GetTextAsync(match.Content);
        return $"{await DescribeSessionsAsync([match], addCards: true, cancellationToken)}\nAbstract: {description}";
    }

    [ExportAIFunction("get_speaker")]
    [Description("Read a current-city speaker's background and full biography.")]
    public async Task<string> GetSpeakerAsync(
        [Description("Speaker ID or name, including a unique first name.")] string speaker, CancellationToken cancellationToken = default)
    {
        var match = await FindSpeakerAsync(speaker, cancellationToken);
        logger.LogInformation("[AI Tool] get_speaker('{SpeakerId}')", match.Id);
        await SetProgressAsync($"Reading biography for {match.Name}...");
        var bio = await database.GetTextAsync(match.Content);
        CheckTurn(cancellationToken);
        return $"ID: {match.Id} | Name: {match.Name} | {match.Tagline}\nBiography: {bio}";
    }

    [ExportAIFunction("list_sessions")]
    [Description("List sessions overlapping a daytime time range. 2 to 4 means 14:00 to 16:00.")]
    public async Task<string> ListSessionsAsync(
        [Description("Start time: HH:mm or a daytime hour.")] string start,
        [Description("End time: HH:mm or a daytime hour.")] string end, CancellationToken cancellationToken = default)
    {
        CheckTurn(cancellationToken);
        logger.LogInformation("[AI Tool] list_sessions(start: '{Start}', end: '{End}')", start, end);
        await SetProgressAsync($"Listing sessions between {start} and {end}...");
        var from = ParseTime(start);
        var to = ParseTime(end);
        if (to <= from)
            throw new ArgumentException("The end must be after the start on the same day.");
        var sessions = await database.GetSessionsAsync(city!.Id);
        return await DescribeSessionsAsync(sessions.Where(s =>
            TimeOnly.FromDateTime(s.Start.DateTime) < to && TimeOnly.FromDateTime(s.End.DateTime) > from), addCards: true, cancellationToken);
    }

    [ExportAIFunction("get_favorites")]
    [Description("List the user's saved favourites in the current city.")]
    public async Task<string> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        CheckTurn(cancellationToken);
        logger.LogInformation("[AI Tool] get_favorites() in {CityId}", city!.Id);
        await SetProgressAsync("Checking your saved favourites...");
        var sessions = await database.GetSessionsAsync(city.Id);
        return await DescribeSessionsAsync(sessions.Where(s => preferences.IsFavorite(s.Id)), addCards: true, cancellationToken);
    }

    [ExportAIFunction("open_session")]
    [Description("Open a current-city session's detail page only when the user asks to open it. Not needed to favourite it.")]
    public async Task<string> OpenSessionAsync(
        [Description("Session ID or title.")] string session, CancellationToken cancellationToken = default)
    {
        var match = await FindSessionAsync(session, cancellationToken);
        logger.LogInformation("[AI Tool] open_session('{SessionId}')", match.Id);
        await DescribeSessionsAsync([match], addCards: true, cancellationToken);
        await NavigateAsync($"session?id={Uri.EscapeDataString(match.Id)}", cancellationToken);
        return $"Opened {match.Title}.";
    }

    [ExportAIFunction("open_speaker")]
    [Description("Open a current-city speaker's detail page only when the user asks to open it. Use get_speaker to read the bio.")]
    public async Task<string> OpenSpeakerAsync(
        [Description("Speaker ID or name.")] string speaker, CancellationToken cancellationToken = default)
    {
        var match = await FindSpeakerAsync(speaker, cancellationToken);
        logger.LogInformation("[AI Tool] open_speaker('{SpeakerId}')", match.Id);
        await NavigateAsync($"speaker?id={Uri.EscapeDataString(match.Id)}", cancellationToken);
        return $"Opened {match.Name}.";
    }

    [ExportAIFunction("open_event")]
    [Description("Open the Event tab with sponsors, venue details and the map only when the user asks to open it.")]
    public async Task<string> OpenEventAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[AI Tool] open_event()");
        await NavigateAsync("//event", cancellationToken);
        return "Opened the Event and venue tab.";
    }

    [ExportAIFunction("add_favorite", ApprovalRequired = true)]
    [Description("Add a session to favourites after the user approves an inline card.")]
    public Task<string> AddFavoriteAsync(
        [Description("Session ID or title.")] string session, CancellationToken cancellationToken = default) =>
        ChangeFavoriteAsync(session, true, cancellationToken);

    [ExportAIFunction("remove_favorite", ApprovalRequired = true)]
    [Description("Remove a favourite after the user approves an inline card.")]
    public Task<string> RemoveFavoriteAsync(
        [Description("Session ID or title.")] string session, CancellationToken cancellationToken = default) =>
        ChangeFavoriteAsync(session, false, cancellationToken);

    private async Task<string> ChangeFavoriteAsync(string session, bool add, CancellationToken cancellationToken)
    {
        var match = await FindSessionAsync(session, cancellationToken);
        var action = add ? "add_favorite" : "remove_favorite";
        logger.LogInformation("[AI Tool] {Action}('{SessionId}') -> waiting for user approval...", action, match.Id);
        await SetProgressAsync($"Waiting for your approval to {(add ? "add" : "remove")} '{match.Title}'...");
        await DescribeSessionsAsync([match], addCards: true, cancellationToken);
        var approval = new ChatApproval(match, add);
        await MainThread.InvokeOnMainThreadAsync(() => reply!.Approvals.Add(approval));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, turnCancellation);
        // Apple invokes functions natively, bypassing middleware approval handling.
        if (!await approval.WaitAsync(cancellation.Token))
        {
            logger.LogInformation("[AI Tool] {Action}('{SessionId}') denied by user.", action, match.Id);
            return $"Denied by the user. No favourite was changed for {match.Title}. Do not retry.";
        }
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CheckTurn(cancellation.Token);
            preferences.SetFavorite(match.Id, add);
        });
        logger.LogInformation("[AI Tool] {Action}('{SessionId}') approved and saved.", action, match.Id);
        return add ? $"Added {match.Title} to favourites." : $"Removed {match.Title} from favourites.";
    }

    public async Task AddReferencedSessionsAsync(string text, CancellationToken cancellationToken)
    {
        CheckTurn(cancellationToken);
        var sessions = await database.GetSessionsAsync(city!.Id);
        await AddCardsAsync(sessions.Where(s => text.Contains(s.Title, StringComparison.OrdinalIgnoreCase) ||
            text.Contains(s.Id, StringComparison.Ordinal)), cancellationToken);
    }

    private Task SetProgressAsync(string message) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (reply is not null)
                reply.Progress = message;
        });

    private async Task<Session> FindSessionAsync(string value, CancellationToken cancellationToken)
    {
        CheckTurn(cancellationToken);
        var sessions = await database.GetSessionsAsync(city!.Id);
        var match = sessions.FirstOrDefault(s => s.Id.Equals(value, StringComparison.OrdinalIgnoreCase) ||
            s.Title.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;
        var matches = sessions.Where(s => !string.IsNullOrWhiteSpace(value) &&
            s.Title.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] :
            throw new ArgumentException("Use an existing, unambiguous session ID or title from the current city.", nameof(value));
    }

    private async Task<Speaker> FindSpeakerAsync(string value, CancellationToken cancellationToken)
    {
        CheckTurn(cancellationToken);
        var speakers = await database.GetSpeakersAsync(city!.Id);
        var match = speakers.FirstOrDefault(s => s.Id.Equals(value, StringComparison.OrdinalIgnoreCase) ||
            s.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;
        var matches = speakers.Where(s => !string.IsNullOrWhiteSpace(value) &&
            s.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] :
            throw new ArgumentException("Use an existing, unambiguous speaker ID or name from the current city.", nameof(value));
    }

    private async Task<string> DescribeSessionsAsync(IEnumerable<Session> sessions, bool addCards, CancellationToken cancellationToken)
    {
        var matches = sessions.ToArray();
        if (addCards)
            await AddCardsAsync(matches, cancellationToken);
        var conference = await database.GetConferenceAsync();
        return matches.Length == 0 ? $"No matching sessions in {city!.Name}." :
            string.Join('\n', matches.Select(s =>
                $"ID: {s.Id} | {s.Time} | {s.Title} | " +
                $"{string.Join(", ", s.SpeakerIds.Select(id => conference.Speakers.First(p => p.Id == id).Name))} | Room: {s.Track ?? "All attendees"}"));
    }

    private async Task AddCardsAsync(IEnumerable<Session> sessions, CancellationToken cancellationToken)
    {
        var conference = await database.GetConferenceAsync();
        var cards = sessions.Select(s => new ChatSessionCard(s, $"{city!.Name} · {s.Time} · {s.Track ?? "All attendees"}",
            string.Join(", ", s.SpeakerIds.Select(id => conference.Speakers.First(p => p.Id == id).Name)))).ToArray();
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CheckTurn(cancellationToken);
            foreach (var card in cards)
                if (!reply!.Sessions.Any(existing => existing.Session.Id == card.Session.Id))
                    reply.Sessions.Add(card);
        });
    }

    private Task NavigateAsync(string route, CancellationToken cancellationToken) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            CheckTurn(cancellationToken);
            await Shell.Current.GoToAsync(route);
        });

    private void CheckTurn(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        turnCancellation.ThrowIfCancellationRequested();
        if (city is null || reply is null)
            throw new InvalidOperationException("Assistant tools require an active reply.");
        if (preferences.SelectedCityId != selectedCityId)
            throw new InvalidOperationException("The city changed. Ask again using the current city.");
    }

    private static TimeOnly ParseTime(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) && hour is >= 1 and <= 23)
            return new(hour < 8 ? hour + 12 : hour, 0);
        if (TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return time.Hour is >= 1 and <= 7 && !text.EndsWith("am") ? time.AddHours(12) : time;
        throw new ArgumentException("Use a daytime hour or an HH:mm time.", nameof(value));
    }
}
