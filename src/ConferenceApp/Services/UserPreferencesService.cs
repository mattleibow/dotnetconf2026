using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Models;

namespace ConferenceApp.Services;

public record CityChangedMessage;
public record FavoritesChangedMessage;

public class UserPreferencesService
{
    private const string CityKey = "city";
    private const string FavoritesKey = "favorites";

    public string? SelectedCityId
    {
        get => Preferences.Get(CityKey, null);
        set
        {
            if (value == SelectedCityId)
                return;
            if (string.IsNullOrEmpty(value))
                Preferences.Remove(CityKey);
            else
                Preferences.Set(CityKey, value);
            WeakReferenceMessenger.Default.Send(new CityChangedMessage());
        }
    }

    public async Task<City> GetSelectedCityAsync(ConferenceDatabase database) =>
        await database.GetCityAsync(SelectedCityId);

    public async Task<City?> PickCityAsync(ConferenceDatabase database)
    {
        var cities = await database.GetCitiesAsync();
        var names = cities.Select(c => c.Name).ToArray();
        var choice = await Shell.Current.DisplayActionSheetAsync("Select City", "Cancel", null, names);
        if (cities.FirstOrDefault(c => c.Name == choice) is { } city)
        {
            SelectedCityId = city.Id;
            return city;
        }
        return null;
    }

    public IReadOnlySet<string> GetFavoriteIds() =>
        Preferences.Get(FavoritesKey, "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    public bool IsFavorite(string sessionId) => GetFavoriteIds().Contains(sessionId);

    public void SetFavorite(string sessionId, bool isFavorite)
    {
        var ids = GetFavoriteIds().ToHashSet();
        if (isFavorite ? !ids.Add(sessionId) : !ids.Remove(sessionId))
            return;
        Preferences.Set(FavoritesKey, string.Join(',', ids));
        WeakReferenceMessenger.Default.Send(new FavoritesChangedMessage());
    }
}
