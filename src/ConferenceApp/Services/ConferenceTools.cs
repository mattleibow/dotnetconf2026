using System.ComponentModel;
using System.Globalization;
using ConferenceApp.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceApp.Services;

[AIToolSource(typeof(ConferenceTools))]
public partial class ConferenceToolContext : AIToolContext;

public sealed class ConferenceTools(ConferenceService conference, SessionSearchService search, FavoriteApproval approval, ILogger<ConferenceTools> logger)
{
    private readonly object sync = new();
    private readonly Dictionary<string, Session> returnedSessions = [];
    public string CityId { get; set; } = "";

    public Session[] ReturnedSessions
    {
        get { lock (sync) return [.. returnedSessions.Values]; }
    }

    [ExportAIFunction("search_sessions")]
    [Description("Search sessions in the selected city by topic, title or speaker.")]
    public async Task<SessionToolResult[]> SearchSessionsAsync(
        [Description("Topic, title or speaker to search for")] string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        logger.LogInformation("Searching sessions for selected city {CityId}", CityId);
        var results = await search.SearchSessionsAsync(query, CityId);
        if (!string.IsNullOrEmpty(results.Warning))
            logger.LogWarning("Session search: {Warning}", results.Warning);
        return await DescribeSessionsAsync(results.Results.Where(r => r.Session.Kind == "session").Take(5).Select(r => r.Session));
    }

    [ExportAIFunction("get_session")]
    [Description("Get the details of a session in the selected city by its ID or title.")]
    public async Task<SessionToolResult[]> GetSessionAsync(
        [Description("Session ID or title")] string idOrTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrTitle);
        var data = await conference.GetConferenceAsync();
        return await DescribeSessionsAsync(data.Sessions.Where(s => s.CityId == CityId && s.Kind == "session" &&
            (s.Id.Equals(idOrTitle, StringComparison.OrdinalIgnoreCase) || s.Title.Contains(idOrTitle, StringComparison.OrdinalIgnoreCase))));
    }

    [ExportAIFunction("list_sessions_in_time_range")]
    [Description("List sessions in the selected city that overlap a local time range on the conference date.")]
    public async Task<SessionToolResult[]> ListSessionsAsync(
        [Description("Start time in 24-hour HH:mm format")] string start,
        [Description("End time in 24-hour HH:mm format")] string end)
    {
        logger.LogInformation("Listing sessions for selected city {CityId} from {Start} to {End}", CityId, start, end);
        if (!TimeOnly.TryParseExact(start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
            !TimeOnly.TryParseExact(end, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) || from >= to)
            throw new ArgumentException("Use a valid increasing time range in 24-hour HH:mm format.");
        var data = await conference.GetConferenceAsync();
        return await DescribeSessionsAsync(data.Sessions.Where(s => s.CityId == CityId && s.Kind == "session" &&
            TimeOnly.FromDateTime(s.Start.DateTime) < to && TimeOnly.FromDateTime(s.End.DateTime) > from).OrderBy(s => s.Start));
    }

    [ExportAIFunction("get_speaker")]
    [Description("Find a speaker in the selected city and read their biography.")]
    public async Task<SpeakerToolResult[]> GetSpeakerAsync(
        [Description("Speaker name or ID")] string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        logger.LogInformation("Looking up a speaker for selected city {CityId}", CityId);
        var data = await conference.GetConferenceAsync();
        var speakers = data.Speakers.Where(s => s.CityIds.Contains(CityId) &&
            (s.Id.Equals(name, StringComparison.OrdinalIgnoreCase) || s.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
        return await Task.WhenAll(speakers.Select(async s => new SpeakerToolResult(s.Id, s.Name, s.Tagline, await conference.GetContentAsync(s.Content))));
    }

    [ExportAIFunction("my_favourites")]
    [Description("List the user's favourite sessions in the selected city.")]
    public async Task<SessionToolResult[]> GetFavoritesAsync()
    {
        logger.LogInformation("Looking up favourites for selected city {CityId}", CityId);
        var data = await conference.GetConferenceAsync();
        return await DescribeSessionsAsync(data.Sessions.Where(s => s.CityId == CityId && s.Kind == "session" && conference.IsFavorite(s.Id)).OrderBy(s => s.Start));
    }

    [ExportAIFunction("get_venue")]
    [Description("Get the selected city's conference venue and address.")]
    public async Task<Venue> GetVenueAsync()
    {
        var data = await conference.GetConferenceAsync();
        var city = data.Cities.First(c => c.Id == CityId);
        return data.Venues.First(v => v.Id == city.VenueId);
    }

    [ExportAIFunction("get_sponsors")]
    [Description("List the sponsors for the selected city.")]
    public async Task<Sponsor[]> GetSponsorsAsync()
    {
        logger.LogInformation("Looking up sponsors for selected city {CityId}", CityId);
        var data = await conference.GetConferenceAsync();
        return [.. data.Sponsors.Where(s => s.CityIds.Contains(CityId))];
    }

    [ExportAIFunction("open_session")]
    [Description("Open the detail page for a session in the selected city by exact ID or title.")]
    public async Task<string> OpenSessionAsync([Description("Exact session ID or title")] string idOrTitle, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(idOrTitle);
        if (session is null) return "Session not found in the selected city; nothing was opened.";
        return await NavigateAsync($"//agenda", $"session-detail?id={Uri.EscapeDataString(session.Id)}", $"Opened session: {session.Title}.", cancellationToken);
    }

    [ExportAIFunction("open_speaker")]
    [Description("Open a speaker's detail page in the selected city by exact ID or name.")]
    public async Task<string> OpenSpeakerAsync([Description("Exact speaker ID or name")] string idOrName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrName);
        var data = await conference.GetConferenceAsync();
        var matches = data.Speakers.Where(s => s.CityIds.Contains(CityId) &&
            (s.Id.Equals(idOrName, StringComparison.OrdinalIgnoreCase) || s.Name.Equals(idOrName, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (matches.Length != 1) return "Speaker not found uniquely in the selected city; nothing was opened.";
        return await NavigateAsync("//speakers", $"speaker-detail?id={Uri.EscapeDataString(matches[0].Id)}", $"Opened speaker: {matches[0].Name}.", cancellationToken);
    }

    [ExportAIFunction("open_sponsor")]
    [Description("Open a sponsor's detail page in the selected city by exact name.")]
    public async Task<string> OpenSponsorAsync([Description("Exact sponsor name")] string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var data = await conference.GetConferenceAsync();
        var matches = data.Sponsors.Where(s => s.CityIds.Contains(CityId) && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) return "Sponsor not found uniquely in the selected city; nothing was opened.";
        return await NavigateAsync("//venue", $"sponsor-detail?name={Uri.EscapeDataString(matches[0].Name)}", $"Opened sponsor: {matches[0].Name}.", cancellationToken);
    }

    [ExportAIFunction("open_venue")]
    [Description("Open the selected city's venue page.")]
    public Task<string> OpenVenueAsync(CancellationToken cancellationToken) =>
        NavigateAsync("//venue", null, "Opened the venue page.", cancellationToken);

    [ExportAIFunction("add_favourite", ApprovalRequired = true)]
    [Description("Request approval to add a session in the selected city to favourites.")]
    public Task<string> AddFavoriteAsync([Description("Exact session ID or title")] string idOrTitle, CancellationToken cancellationToken) =>
        ChangeFavoriteAsync(idOrTitle, true, cancellationToken);

    [ExportAIFunction("remove_favourite", ApprovalRequired = true)]
    [Description("Request approval to remove a session in the selected city from favourites.")]
    public Task<string> RemoveFavoriteAsync([Description("Exact session ID or title")] string idOrTitle, CancellationToken cancellationToken) =>
        ChangeFavoriteAsync(idOrTitle, false, cancellationToken);

    private async Task<Session?> FindSessionAsync(string idOrTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrTitle);
        var data = await conference.GetConferenceAsync();
        var matches = data.Sessions.Where(s => s.CityId == CityId && s.Kind == "session" &&
            (s.Id.Equals(idOrTitle, StringComparison.OrdinalIgnoreCase) || s.Title.Equals(idOrTitle, StringComparison.OrdinalIgnoreCase))).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private async Task<string> ChangeFavoriteAsync(string idOrTitle, bool favorite, CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(idOrTitle);
        if (session is null) return "Session not found uniquely in the selected city; favourites were not changed.";
        if (conference.CityId != CityId) return "The selected city changed; favourites were not changed.";
        if (conference.IsFavorite(session.Id) == favorite)
            return favorite ? $"{session.Title} is already a favourite; nothing changed." : $"{session.Title} is not a favourite; nothing changed.";

        var approved = await approval.RequestAsync($"{(favorite ? "Add" : "Remove")} “{session.Title}” {(favorite ? "to" : "from")} favourites?", cancellationToken);
        return await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!approved || cancellationToken.IsCancellationRequested)
                return $"The request to {(favorite ? "add" : "remove")} {session.Title} was denied or cancelled; favourites were not changed.";
            if (conference.CityId != CityId)
                return "The selected city changed; favourites were not changed.";
            conference.SetFavorite(session.Id, favorite);
            return favorite ? $"Added {session.Title} to favourites." : $"Removed {session.Title} from favourites.";
        });
    }

    private Task<string> NavigateAsync(string root, string? detail, string success, CancellationToken cancellationToken) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (conference.CityId != CityId) return "The selected city changed; nothing was opened.";
            await Shell.Current.GoToAsync(root);
            cancellationToken.ThrowIfCancellationRequested();
            if (detail is not null) await Shell.Current.GoToAsync(detail);
            return success;
        });

    private async Task<SessionToolResult[]> DescribeSessionsAsync(IEnumerable<Session> sessions)
    {
        var data = await conference.GetConferenceAsync();
        var items = sessions.ToArray();
        var results = await Task.WhenAll(items.Select(async s => new SessionToolResult(
            s.Id, s.Title, $"{s.Start:HH:mm}", $"{s.End:HH:mm}", s.Track,
            string.Join(", ", data.Speakers.Where(p => s.SpeakerIds.Contains(p.Id)).Select(p => p.Name)),
            await conference.GetContentAsync(s.Content))));
        lock (sync)
            foreach (var session in items)
                returnedSessions.TryAdd(session.Id, session);
        return results;
    }
}

public sealed record SessionToolResult(string Id, string Title, string Start, string End, string? Track, string Speakers, string Abstract);
public sealed record SpeakerToolResult(string Id, string Name, string? Tagline, string Biography);
