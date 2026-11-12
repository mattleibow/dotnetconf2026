using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Models;

namespace ConferenceApp.Services;

public record CityChangedMessage;
public record FavoritesChangedMessage;

public class UserPreferencesService
{
    private const string CityKey = "selected_city";
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

    public async Task<City> GetSelectedCityAsync(ConferenceDatabase database)
    {
        var city = await database.GetCityAsync(SelectedCityId);
        if (SelectedCityId != city.Id)
            Preferences.Set(CityKey, city.Id);
        return city;
    }

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

    public void SetFavorite(string sessionId, bool isFavorite) =>
        SetFavorites([sessionId], isFavorite);

    public void SetFavorites(IEnumerable<string> sessionIds, bool isFavorite)
    {
        var ids = GetFavoriteIds().ToHashSet();
        var changed = false;
        foreach (var sessionId in sessionIds)
            changed |= isFavorite ? ids.Add(sessionId) : ids.Remove(sessionId);
        if (!changed)
            return;
        Preferences.Set(FavoritesKey, string.Join(',', ids));
        WeakReferenceMessenger.Default.Send(new FavoritesChangedMessage());
    }
}
