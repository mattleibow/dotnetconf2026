using System.Text.Json;

namespace ConferenceApp.Services;

public sealed class FavoritesService
{
    private readonly HashSet<string> favorites =
        JsonSerializer.Deserialize<HashSet<string>>(Preferences.Default.Get("favorites", "[]")) ?? [];

    public event EventHandler? Changed;

    public bool Contains(string id) => favorites.Contains(id);

    public void Toggle(string id)
    {
        Set(id, !Contains(id));
    }

    public void Set(string id, bool isFavorite)
    {
        var updated = new HashSet<string>(favorites);
        var changed = isFavorite ? updated.Add(id) : updated.Remove(id);
        if (!changed)
            return;
        Preferences.Default.Set("favorites", JsonSerializer.Serialize(updated));
        if (isFavorite)
            favorites.Add(id);
        else
            favorites.Remove(id);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
