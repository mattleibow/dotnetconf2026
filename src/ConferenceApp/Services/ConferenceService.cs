using System.Text.Json;
using ConferenceApp.Models;

namespace ConferenceApp.Services;

public sealed class ConferenceService
{
    private Conference? conference;
    public event EventHandler? Changed;

    public string CityId
    {
        get => Preferences.Default.Get("city", "durban");
        set
        {
            Preferences.Default.Set("city", value);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<Conference> GetConferenceAsync()
    {
        if (conference is not null)
            return conference;
        using var stream = await FileSystem.OpenAppPackageFileAsync("conference/conference.json");
        conference = await JsonSerializer.DeserializeAsync<Conference>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("Conference data is empty.");
        return conference;
    }

    public async Task<string> GetContentAsync(string path)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync($"conference/{path}");
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        var firstLine = text.IndexOf('\n');
        return firstLine < 0 ? "" : text[(firstLine + 1)..].Trim();
    }

    public ImageSource? Image(string? path) => path is null ? null :
        ImageSource.FromStream(async cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await FileSystem.OpenAppPackageFileAsync($"conference/{path}");
        });

    public bool IsFavorite(string id) => Preferences.Default.Get($"favorite-{id}", false);

    public void ToggleFavorite(string id)
    {
        Preferences.Default.Set($"favorite-{id}", !IsFavorite(id));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
