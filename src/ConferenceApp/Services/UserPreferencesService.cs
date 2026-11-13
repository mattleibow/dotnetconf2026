using CommunityToolkit.Mvvm.Messaging;
using ConferenceApp.Data;

namespace ConferenceApp.Services;

public record CityChangedMessage;
public record FavoritesChangedMessage;

public class UserPreferencesService(ConferenceDbProvider dbProvider)
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

    public async Task<City> GetSelectedCityAsync()
    {
        var db = await dbProvider.GetDatabaseAsync();
        var city = db.GetCity(SelectedCityId);
        if (SelectedCityId != city.Id)
            Preferences.Set(CityKey, city.Id);
        return city;
    }

    public async Task<City?> PickCityAsync()
    {
        var db = await dbProvider.GetDatabaseAsync();
        var names = db.Cities.Select(c => c.Name).ToArray();
        var choice = await Shell.Current.DisplayActionSheetAsync("Select City", "Cancel", null, names);
        if (db.Cities.FirstOrDefault(c => c.Name == choice) is { } city)
        {
            SelectedCityId = city.Id;
            return city;
        }
        return null;
    }

    public IReadOnlySet<string> GetFavoriteIds() =>
        Preferences.Get(FavoritesKey, "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

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
