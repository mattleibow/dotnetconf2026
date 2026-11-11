namespace ConferenceApp.Services;

public class UserPreferencesService
{
    private const string CityKey = "city";
    private const string FavoritesKey = "favorites";

    public event EventHandler? CityChanged;
    public event EventHandler? FavoritesChanged;

    public string SelectedCityId
    {
        get => Preferences.Get(CityKey, "durban");
        set
        {
            if (value == SelectedCityId)
                return;
            Preferences.Set(CityKey, value);
            CityChanged?.Invoke(this, EventArgs.Empty);
        }
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
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }
}
