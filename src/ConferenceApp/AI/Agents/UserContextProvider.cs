using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Agents.AI;

namespace ConferenceApp.AI;

/// <summary>
/// Context Provider 1: Injects the user's current city, event date, current time, venue, and saved favourites.
/// </summary>
public sealed class UserContextProvider(IServiceProvider services) : AIContextProvider
{
    private readonly ConferenceDbProvider dbProvider = services.GetRequiredService<ConferenceDbProvider>();
    private readonly UserPreferencesService preferences = services.GetRequiredService<UserPreferencesService>();

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var favoriteTitles = db.Sessions
            .Where(s => s.CityId == city.Id && preferences.IsFavorite(s.Id))
            .Select(s =>
            {
                var tag = !string.IsNullOrWhiteSpace(s.Topic) ? $" [{s.Topic}]" : "";
                return $"{s.Time}{tag} {s.Title}";
            })
            .ToArray();

        return new AIContext
        {
            Instructions = $"""
                CURRENT USER & EVENT CONTEXT
                Event: {db.Name}
                Selected City: {city.Name} | Event Date: {city.Date:dddd, yyyy-MM-dd}
                Current Conference Time: 08:30 (Morning before sessions start)
                Venue: {city.Venue.Name} ({city.Venue.Address})
                Saved Favourites: {(favoriteTitles.Length > 0 ? string.Join("; ", favoriteTitles) : "None yet")}
                """
        };
    }
}
