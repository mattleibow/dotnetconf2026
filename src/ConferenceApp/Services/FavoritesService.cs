namespace ConferenceApp.Services;

public class FavoritesService
{
    private const string Key = "favorites";

    public event EventHandler? FavoritesChanged;

    public IReadOnlySet<string> GetFavoriteIds() =>
        Preferences.Get(Key, "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    public bool IsFavorite(string sessionId) => GetFavoriteIds().Contains(sessionId);

    public void SetFavorite(string sessionId, bool isFavorite)
    {
        var ids = GetFavoriteIds().ToHashSet();
        if (isFavorite ? !ids.Add(sessionId) : !ids.Remove(sessionId))
            return;
        Preferences.Set(Key, string.Join(',', ids));
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }
}
