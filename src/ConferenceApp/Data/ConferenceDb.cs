using System.Text.Json;

namespace ConferenceApp.Data;

public class ConferenceDb(
    string name,
    string timeZone,
    IReadOnlyList<City> cities,
    IReadOnlyList<Session> sessions,
    IReadOnlyList<Speaker> speakers,
    IReadOnlyList<Venue> venues,
    IReadOnlyList<Sponsor> sponsors)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name { get; } = name;
    public string TimeZone { get; } = timeZone;
    public IReadOnlyList<City> Cities { get; } = cities;
    public IReadOnlyList<Session> Sessions { get; } = sessions;
    public IReadOnlyList<Speaker> Speakers { get; } = speakers;
    public IReadOnlyList<Venue> Venues { get; } = venues;
    public IReadOnlyList<Sponsor> Sponsors { get; } = sponsors;

    public City GetCity(string? cityId = null)
    {
        if (!string.IsNullOrEmpty(cityId) && Cities.FirstOrDefault(c => c.Id == cityId) is { } match)
            return match;

        var today = DateOnly.FromDateTime(DateTime.Now);
        return Cities.FirstOrDefault(c => c.Date >= today) ?? Cities[^1];
    }

    internal static async Task<ConferenceDb> LoadAsync()
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync("conference/conference.json");
        var raw = await JsonSerializer.DeserializeAsync<ConferenceDb>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The conference data is empty.");

        var citiesById = raw.Cities.ToDictionary(c => c.Id);
        var speakersById = raw.Speakers.ToDictionary(s => s.Id);
        var venuesById = raw.Venues.ToDictionary(v => v.Id);

        foreach (var city in raw.Cities)
            city.Venue = venuesById[city.VenueId];

        foreach (var venue in raw.Venues)
            venue.Description = await ReadMarkdownBodyAsync(venue.Content);

        foreach (var speaker in raw.Speakers)
            speaker.Bio = await ReadMarkdownBodyAsync(speaker.Content);

        foreach (var session in raw.Sessions)
        {
            session.City = citiesById[session.CityId];
            session.Speakers = [.. session.SpeakerIds.Select(id => speakersById[id])];
            session.Description = await ReadMarkdownBodyAsync(session.Content);
        }

        return new ConferenceDb(
            raw.Name,
            raw.TimeZone,
            [.. raw.Cities.OrderBy(c => c.Date)],
            [.. raw.Sessions.OrderBy(s => s.Start)],
            [.. raw.Speakers.OrderBy(s => s.Name)],
            raw.Venues,
            raw.Sponsors);
    }

    // Session, speaker and venue Markdown files start with a "# Title" heading line.
    private static async Task<string> ReadMarkdownBodyAsync(string path)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync($"conference/{path}");
        using var reader = new StreamReader(stream);
        var lines = (await reader.ReadToEndAsync()).Split('\n');
        return string.Join('\n', lines.SkipWhile(l => l.StartsWith("# ") || l.Trim().Length == 0)).Trim();
    }
}
