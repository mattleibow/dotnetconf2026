using System.Text.Json;

namespace ConferenceApp.Data;

public class ConferenceDatabase(
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

    internal static async Task<ConferenceDatabase> LoadAsync()
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync("conference/conference.json");
        var conference = await JsonSerializer.DeserializeAsync<Conference>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The conference data is empty.");

        var citiesById = conference.Cities.ToDictionary(c => c.Id);
        var speakersById = conference.Speakers.ToDictionary(s => s.Id);
        var venuesById = conference.Venues.ToDictionary(v => v.Id);

        foreach (var city in conference.Cities)
            city.Venue = venuesById[city.VenueId];

        foreach (var venue in conference.Venues)
            venue.Description = await ReadMarkdownBodyAsync(venue.Content);

        foreach (var speaker in conference.Speakers)
            speaker.Bio = await ReadMarkdownBodyAsync(speaker.Content);

        foreach (var session in conference.Sessions)
        {
            session.City = citiesById[session.CityId];
            session.Speakers = [.. session.SpeakerIds.Select(id => speakersById[id])];
            session.Description = await ReadMarkdownBodyAsync(session.Content);
        }

        return new ConferenceDatabase(
            conference.Name,
            conference.TimeZone,
            [.. conference.Cities.OrderBy(c => c.Date)],
            [.. conference.Sessions.OrderBy(s => s.Start)],
            [.. conference.Speakers.OrderBy(s => s.Name)],
            conference.Venues,
            conference.Sponsors);
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
