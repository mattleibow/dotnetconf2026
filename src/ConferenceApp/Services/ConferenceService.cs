using System.Text.Json;
using ConferenceApp.Models;

namespace ConferenceApp.Services;

public class ConferenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private Conference? conference;

    public event EventHandler? CityChanged;

    public string SelectedCityId
    {
        get => Preferences.Get("city", "durban");
        set
        {
            if (value == SelectedCityId)
                return;
            Preferences.Set("city", value);
            CityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

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

    public async Task<City> GetSelectedCityAsync() =>
        (await GetConferenceAsync()).Cities.First(c => c.Id == SelectedCityId);

    public async Task<IReadOnlyList<Session>> GetSessionsAsync() =>
        [.. (await GetConferenceAsync()).Sessions.Where(s => s.CityId == SelectedCityId).OrderBy(s => s.Start)];

    public async Task<Session?> GetSessionAsync(string id) =>
        (await GetConferenceAsync()).Sessions.FirstOrDefault(s => s.Id == id);

    public async Task<IReadOnlyList<Speaker>> GetSpeakersAsync() =>
        [.. (await GetConferenceAsync()).Speakers.Where(s => s.CityIds.Contains(SelectedCityId)).OrderBy(s => s.Name)];

    public async Task<Speaker?> GetSpeakerAsync(string id) =>
        (await GetConferenceAsync()).Speakers.FirstOrDefault(s => s.Id == id);

    public async Task<IReadOnlyList<Speaker>> GetSpeakersForSessionAsync(Session session)
    {
        var speakers = (await GetConferenceAsync()).Speakers;
        return [.. session.SpeakerIds.Select(id => speakers.First(s => s.Id == id))];
    }

    public async Task<IReadOnlyList<Session>> GetSessionsForSpeakerAsync(string speakerId) =>
        [.. (await GetSessionsAsync()).Where(s => s.SpeakerIds.Contains(speakerId))];

    public async Task<Venue> GetVenueAsync()
    {
        var data = await GetConferenceAsync();
        var city = await GetSelectedCityAsync();
        return data.Venues.First(v => v.Id == city.VenueId);
    }

    public async Task<IReadOnlyList<Sponsor>> GetSponsorsAsync() =>
        [.. (await GetConferenceAsync()).Sponsors.Where(s => s.CityIds.Contains(SelectedCityId))];

    // Session, speaker and venue descriptions are Markdown files that start with a "# Title" line.
    public async Task<string> GetTextAsync(string path)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync($"conference/{path}");
        using var reader = new StreamReader(stream);
        var lines = (await reader.ReadToEndAsync()).Split('\n');
        return string.Join('\n', lines.SkipWhile(l => l.StartsWith("# ") || l.Trim().Length == 0)).Trim();
    }
}
