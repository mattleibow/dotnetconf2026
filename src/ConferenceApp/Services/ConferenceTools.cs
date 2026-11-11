using System.ComponentModel;
using System.Globalization;
using ConferenceApp.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceApp.Services;

[AIToolSource(typeof(ConferenceTools))]
public partial class ConferenceToolContext : AIToolContext;

public sealed class ConferenceTools(ConferenceService conference, SessionSearchService search, ILogger<ConferenceTools> logger)
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
        return await Task.WhenAll(speakers.Select(async s => new SpeakerToolResult(s.Name, s.Tagline, await conference.GetContentAsync(s.Content))));
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
public sealed record SpeakerToolResult(string Name, string? Tagline, string Biography);
