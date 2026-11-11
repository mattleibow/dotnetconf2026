using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceApp.Services;

[AIToolSource(typeof(ConferenceTools))]
public partial class AssistantTools : AIToolContext;

public sealed class ConferenceTools(ConferenceService data, CityService city, FavoritesService favorites, SessionSearchService search, FavoriteApproval approval)
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
            result.AppendLine($"{speaker.Id} | {speaker.Name} | {speaker.Tagline}\n{await data.GetContentAsync(speaker.Content)}");
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
        return Results(conference.Sponsors.Where(s => s.CityIds.Contains(city.SelectedId)).Select(s => $"{s.Id} | {s.Name} | {s.Tier}"));
    }

    [ExportAIFunction("open_session")]
    [Description("Open a session's detail page by ID or exact title in the selected city.")]
    public async Task<string> OpenSessionAsync(
        [Description("Session ID or exact title")] string session, CancellationToken cancellationToken)
    {
        var match = await FindSessionAsync(session);
        if (match is null)
            return "Session not found in the selected city.";
        await NavigateAsync($"session?id={Uri.EscapeDataString(match.Id)}", cancellationToken);
        return $"Opened session: {match.Title}.";
    }

    [ExportAIFunction("open_speaker")]
    [Description("Open a speaker's detail page by ID or exact name in the selected city.")]
    public async Task<string> OpenSpeakerAsync(
        [Description("Speaker ID or exact name")] string speaker, CancellationToken cancellationToken)
    {
        var conference = await data.GetConferenceAsync();
        var match = conference.Speakers.FirstOrDefault(s => s.CityIds.Contains(city.SelectedId) &&
            (s.Id.Equals(speaker, StringComparison.OrdinalIgnoreCase) || s.Name.Equals(speaker, StringComparison.OrdinalIgnoreCase)));
        if (match is null)
            return "Speaker not found in the selected city.";
        await NavigateAsync($"speaker?id={Uri.EscapeDataString(match.Id)}", cancellationToken);
        return $"Opened speaker: {match.Name}.";
    }

    [ExportAIFunction("open_sponsor")]
    [Description("Open a sponsor's detail page by ID or exact name in the selected city.")]
    public async Task<string> OpenSponsorAsync(
        [Description("Sponsor ID or exact name")] string sponsor, CancellationToken cancellationToken)
    {
        var conference = await data.GetConferenceAsync();
        var match = conference.Sponsors.FirstOrDefault(s => s.CityIds.Contains(city.SelectedId) &&
            (s.Id.Equals(sponsor, StringComparison.OrdinalIgnoreCase) || s.Name.Equals(sponsor, StringComparison.OrdinalIgnoreCase)));
        if (match is null)
            return "Sponsor not found in the selected city.";
        await NavigateAsync($"sponsor?id={Uri.EscapeDataString(match.Id)}", cancellationToken);
        return $"Opened sponsor: {match.Name}.";
    }

    [ExportAIFunction("open_venue")]
    [Description("Open the venue page for the selected city.")]
    public async Task<string> OpenVenueAsync(CancellationToken cancellationToken)
    {
        await NavigateAsync("//explore", cancellationToken);
        return "Opened the venue and sponsors page.";
    }

    [ExportAIFunction("add_favourite", ApprovalRequired = true)]
    [Description("Set a session as a favourite, with user approval.")]
    public Task<string> AddFavouriteAsync(
        [Description("Session ID or exact title")] string session, CancellationToken cancellationToken) =>
        SetFavouriteAsync(session, true, cancellationToken);

    [ExportAIFunction("remove_favourite", ApprovalRequired = true)]
    [Description("Remove a session from favourites, with user approval.")]
    public Task<string> RemoveFavouriteAsync(
        [Description("Session ID or exact title")] string session, CancellationToken cancellationToken) =>
        SetFavouriteAsync(session, false, cancellationToken);

    private async Task<string> SetFavouriteAsync(string session, bool isFavourite, CancellationToken cancellationToken)
    {
        var cityId = city.SelectedId;
        var match = await FindSessionAsync(session);
        if (match is null || match.Kind == "general")
            return await RecordResultAsync("A favouritable session was not found in the selected city; favourites were not changed.");

        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (city.SelectedId != cityId)
                return RecordResult("The selected city changed; favourites were not changed.");
            if (approval.TryGetDecision(match.Id, isFavourite, out var previousResult))
                return previousResult;
            if (favorites.Contains(match.Id) == isFavourite)
                return RecordResult(isFavourite
                    ? $"{match.Title} is already in favourites; nothing changed."
                    : $"{match.Title} is not in favourites; nothing changed.");
            await Shell.Current.GoToAsync("//assistant");
            cancellationToken.ThrowIfCancellationRequested();
            var action = isFavourite ? "Add to" : "Remove from";
            var approved = await approval.RequestAsync($"{action} favourites: {match.Title} ({match.Start:HH:mm}, {match.Track ?? "Conference"})?", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!approved)
                return approval.RecordDecision(match.Id, isFavourite, $"Denied. Favourites were not changed for {match.Title}.");
            if (city.SelectedId != cityId)
                return RecordResult("The selected city changed; favourites were not changed.");
            favorites.Set(match.Id, isFavourite);
            return RecordResult(isFavourite ? $"Added {match.Title} to favourites." : $"Removed {match.Title} from favourites.");
        });
    }

    private Task<string> RecordResultAsync(string result) =>
        MainThread.InvokeOnMainThreadAsync(() => RecordResult(result));

    private string RecordResult(string result)
    {
        approval.RecordResult(result);
        return result;
    }

    private async Task<Models.Session?> FindSessionAsync(string session)
    {
        var conference = await data.GetConferenceAsync();
        return conference.Sessions.FirstOrDefault(s => s.CityId == city.SelectedId &&
            (s.Id.Equals(session, StringComparison.OrdinalIgnoreCase) || s.Title.Equals(session, StringComparison.OrdinalIgnoreCase)));
    }

    private static Task NavigateAsync(string route, CancellationToken cancellationToken) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Shell.Current.GoToAsync(route);
        });

    private static string FormatSession(Models.Session session) =>
        $"{session.Id} | {session.Start:HH:mm}-{session.End:HH:mm} | {session.Track} | {session.Title}";

    private static string Results(IEnumerable<string> items)
    {
        var text = string.Join("\n", items);
        return text.Length == 0 ? "No matches in the selected city." : text;
    }
}
