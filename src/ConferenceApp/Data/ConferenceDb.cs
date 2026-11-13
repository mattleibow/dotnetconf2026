using System.Text.Json;

namespace ConferenceApp.Data;

public class ConferenceDb
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name { get; set; } = "";

    public string TimeZone { get; set; } = "";
    
    public List<City> Cities { get; set; } = [];
    
    public List<Session> Sessions { get; set; } = [];
    
    public List<Speaker> Speakers { get; set; } = [];
    
    public List<Venue> Venues { get; set; } = [];
    
    public List<Sponsor> Sponsors { get; set; } = [];

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
        var db = await JsonSerializer.DeserializeAsync<ConferenceDb>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The conference data is empty.");

        var citiesById = db.Cities.ToDictionary(c => c.Id);
        var speakersById = db.Speakers.ToDictionary(s => s.Id);
        var venuesById = db.Venues.ToDictionary(v => v.Id);

        foreach (var city in db.Cities)
            city.Venue = venuesById[city.VenueId];

        foreach (var venue in db.Venues)
            venue.Description = await ReadMarkdownBodyAsync(venue.Content);

        foreach (var speaker in db.Speakers)
            speaker.Bio = await ReadMarkdownBodyAsync(speaker.Content);

        foreach (var session in db.Sessions)
        {
            session.City = citiesById[session.CityId];
            session.Speakers = [.. session.SpeakerIds.Select(id => speakersById[id])];
            session.Description = await ReadMarkdownBodyAsync(session.Content);
        }

        db.Cities.Sort((a, b) => a.Date.CompareTo(b.Date));
        db.Sessions.Sort((a, b) => a.Start.CompareTo(b.Start));
        db.Speakers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        return db;
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
