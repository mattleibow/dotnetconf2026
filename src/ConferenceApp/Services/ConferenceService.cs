using System.Text.Json;
using ConferenceApp.Models;

namespace ConferenceApp.Services;

public sealed class ConferenceService
{
    private Conference? conference;
    private readonly SemaphoreSlim loadLock = new(1, 1);

    public async Task<Conference> GetConferenceAsync()
    {
        if (conference is not null)
            return conference;

        await loadLock.WaitAsync();
        try
        {
            if (conference is not null)
                return conference;

            using var stream = await FileSystem.OpenAppPackageFileAsync("conference/conference.json");
            conference = await JsonSerializer.DeserializeAsync<Conference>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException("The conference snapshot is empty.");
            return conference;
        }
        finally
        {
            loadLock.Release();
        }
    }

    public async Task<string> GetContentAsync(string path)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync($"conference/{path}");
        using var reader = new StreamReader(stream);
        var markdown = await reader.ReadToEndAsync();
        var firstNewline = markdown.IndexOf('\n');
        return firstNewline < 0 ? "" : markdown[(firstNewline + 1)..].Trim();
    }

    public static ImageSource? Image(string? path) => path is null
        ? null
        : ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync($"conference/{path}"));
}
