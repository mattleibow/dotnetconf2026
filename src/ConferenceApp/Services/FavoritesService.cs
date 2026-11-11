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
        if (!favorites.Remove(id))
            favorites.Add(id);
        Preferences.Default.Set("favorites", JsonSerializer.Serialize(favorites));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
