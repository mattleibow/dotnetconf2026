using System.Text.Json;

namespace ConferenceApp.Data;

/// <summary>
/// Provides on-demand initialization of the in-memory ConferenceDatabase,
/// loading conference.json, caching all Markdown descriptions, and linking navigation properties.
/// </summary>
public class ConferenceDbProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Lazy<Task<ConferenceDatabase>> database = new(LoadDatabaseAsync);

    public Task<ConferenceDatabase> GetDatabaseAsync() => database.Value;

    private static async Task<ConferenceDatabase> LoadDatabaseAsync()
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
