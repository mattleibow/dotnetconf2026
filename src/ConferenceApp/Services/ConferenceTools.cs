using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceApp.Services;

[AIToolSource(typeof(ConferenceTools))]
public partial class AssistantTools : AIToolContext;

public sealed class ConferenceTools(ConferenceService data, CityService city, FavoritesService favorites, SessionSearchService search)
{
    [ExportAIFunction("search_sessions")]
    [Description("Search sessions in the selected city by topic, title or speaker.")]
    public async Task<string> SearchSessionsAsync(
        [Description("A topic, session title or speaker name")] string query)
    {
        var results = await search.SearchSessionsAsync(query);
        return Results(results.Take(5).Select(r => FormatSession(r.Session)));
    }

    [ExportAIFunction("get_session")]
    [Description("Get the full description of a session in the selected city by ID or exact title.")]
    public async Task<string> GetSessionAsync(
        [Description("Session ID or exact session title")] string session)
    {
        var conference = await data.GetConferenceAsync();
        var match = conference.Sessions.FirstOrDefault(s => s.CityId == city.SelectedId &&
            (s.Id.Equals(session, StringComparison.OrdinalIgnoreCase) || s.Title.Equals(session, StringComparison.OrdinalIgnoreCase)));
        return match is null ? "Session not found in the selected city." : $"{FormatSession(match)}\n{await data.GetContentAsync(match.Content)}";
    }

    [ExportAIFunction("sessions_in_time_range")]
    [Description("List sessions overlapping a local time range on the selected city's event day.")]
    public async Task<string> SessionsInTimeRangeAsync(
        [Description("Start time in 24-hour HH:mm format")] string start,
        [Description("End time in 24-hour HH:mm format")] string end)
    {
        if (!TimeOnly.TryParseExact(start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
            !TimeOnly.TryParseExact(end, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) || from >= to)
            return "Use a valid local time range in HH:mm format, with the start before the end.";
        var conference = await data.GetConferenceAsync();
        return Results(conference.Sessions.Where(s => s.CityId == city.SelectedId &&
            TimeOnly.FromDateTime(s.Start.DateTime) < to && TimeOnly.FromDateTime(s.End.DateTime) > from)
            .OrderBy(s => s.Start).Select(FormatSession));
    }

    [ExportAIFunction("get_speaker")]
    [Description("Find a speaker in the selected city by name or ID and read their biography.")]
    public async Task<string> GetSpeakerAsync(
        [Description("Speaker name or ID")] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Provide a speaker name or ID.";
        var conference = await data.GetConferenceAsync();
        var matches = conference.Speakers.Where(s => s.CityIds.Contains(city.SelectedId) &&
            (s.Id.Equals(name, StringComparison.OrdinalIgnoreCase) || s.Name.Contains(name, StringComparison.OrdinalIgnoreCase))).ToList();
        if (matches.Count == 0)
            return "Speaker not found in the selected city.";
        var result = new StringBuilder();
        foreach (var speaker in matches)
            result.AppendLine($"{speaker.Name} | {speaker.Tagline}\n{await data.GetContentAsync(speaker.Content)}");
        return result.ToString();
    }

    [ExportAIFunction("my_favourites")]
    [Description("List the user's favourite sessions in the selected city.")]
    public async Task<string> MyFavouritesAsync()
    {
        var conference = await data.GetConferenceAsync();
        return Results(conference.Sessions.Where(s => s.CityId == city.SelectedId && favorites.Contains(s.Id))
            .OrderBy(s => s.Start).Select(FormatSession));
    }

    [ExportAIFunction("get_venue")]
    [Description("Get the venue and address for the selected city.")]
    public async Task<string> GetVenueAsync()
    {
        var conference = await data.GetConferenceAsync();
        var selected = conference.Cities.First(c => c.Id == city.SelectedId);
        var venue = conference.Venues.First(v => v.Id == selected.VenueId);
        return $"{venue.Name} | {venue.Address}\n{await data.GetContentAsync(venue.Content)}";
    }

    [ExportAIFunction("get_sponsors")]
    [Description("List sponsors for the selected city.")]
    public async Task<string> GetSponsorsAsync()
    {
        var conference = await data.GetConferenceAsync();
        return Results(conference.Sponsors.Where(s => s.CityIds.Contains(city.SelectedId)).Select(s => $"{s.Name} | {s.Tier}"));
    }

    private static string FormatSession(Models.Session session) =>
        $"{session.Id} | {session.Start:HH:mm}-{session.End:HH:mm} | {session.Track} | {session.Title}";

    private static string Results(IEnumerable<string> items)
    {
        var text = string.Join("\n", items);
        return text.Length == 0 ? "No matches in the selected city." : text;
    }
}
