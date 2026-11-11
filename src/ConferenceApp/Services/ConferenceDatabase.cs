using System.Text.Json;
using ConferenceApp.Models;

namespace ConferenceApp.Services;

public class ConferenceDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private Conference? conference;

    public async Task<Conference> GetConferenceAsync()
    {
        if (conference is null)
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("conference/conference.json");
            conference = await JsonSerializer.DeserializeAsync<Conference>(stream, JsonOptions)
                ?? throw new InvalidOperationException("The conference data is empty.");
        }
        return conference;
    }

    public async Task<IReadOnlyList<City>> GetCitiesAsync() =>
        (await GetConferenceAsync()).Cities;

    public async Task<City> GetCityAsync(string cityId) =>
        (await GetConferenceAsync()).Cities.First(c => c.Id == cityId);

    public async Task<IReadOnlyList<Session>> GetSessionsAsync(string cityId) =>
        [.. (await GetConferenceAsync()).Sessions.Where(s => s.CityId == cityId).OrderBy(s => s.Start)];

    public async Task<Session?> GetSessionAsync(string id) =>
        (await GetConferenceAsync()).Sessions.FirstOrDefault(s => s.Id == id);

    public async Task<IReadOnlyList<Speaker>> GetSpeakersAsync(string cityId) =>
        [.. (await GetConferenceAsync()).Speakers.Where(s => s.CityIds.Contains(cityId)).OrderBy(s => s.Name)];

    public async Task<Speaker?> GetSpeakerAsync(string id) =>
        (await GetConferenceAsync()).Speakers.FirstOrDefault(s => s.Id == id);

    public async Task<IReadOnlyList<Speaker>> GetSpeakersForSessionAsync(Session session)
    {
        var speakers = (await GetConferenceAsync()).Speakers;
        return [.. session.SpeakerIds.Select(id => speakers.First(s => s.Id == id))];
    }

    public async Task<IReadOnlyList<Session>> GetSessionsForSpeakerAsync(string cityId, string speakerId) =>
        [.. (await GetSessionsAsync(cityId)).Where(s => s.SpeakerIds.Contains(speakerId))];

    public async Task<Venue> GetVenueAsync(string cityId)
    {
        var data = await GetConferenceAsync();
        var city = await GetCityAsync(cityId);
        return data.Venues.First(v => v.Id == city.VenueId);
    }

    public async Task<IReadOnlyList<Sponsor>> GetSponsorsAsync(string cityId) =>
        [.. (await GetConferenceAsync()).Sponsors.Where(s => s.CityIds.Contains(cityId))];

    // Session, speaker and venue descriptions are Markdown files that start with a "# Title" line.
    public async Task<string> GetTextAsync(string path)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync($"conference/{path}");
        using var reader = new StreamReader(stream);
        var lines = (await reader.ReadToEndAsync()).Split('\n');
        return string.Join('\n', lines.SkipWhile(l => l.StartsWith("# ") || l.Trim().Length == 0)).Trim();
    }
}
