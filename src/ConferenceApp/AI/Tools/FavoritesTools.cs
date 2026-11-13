using System.ComponentModel;
using ConferenceApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceApp.AI;

[AIToolSource(typeof(FavoritesTools))]
public partial class FavoritesToolContext : AIToolContext;

/// <summary>
/// Tools for reading and updating the user's saved favourite sessions.
/// </summary>
public sealed class FavoritesTools(
    ConferenceDatabase database,
    UserPreferencesService preferences,
    ILogger<FavoritesTools> logger)
{
    [ExportAIFunction("get_favorites")]
    [Description("List the user's saved favourite sessions in the current city.")]
    public async Task<string> GetFavoritesAsync(
        [FromServices] ConferenceDataTools dataTools,
        CancellationToken cancellationToken = default)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        logger.LogInformation("[AI Tool] get_favorites() in {CityId}", city.Id);

        var sessions = await database.GetSessionsAsync(city.Id);
        return await dataTools.FormatSessionsAsync(city, sessions.Where(s => preferences.IsFavorite(s.Id)));
    }

    [ExportAIFunction("add_favorite")]
    [Description("Add a session to the user's saved favourites in the current city.")]
    public Task<string> AddFavoriteAsync(
        [Description("Session ID or title.")] string session,
        CancellationToken cancellationToken = default) =>
        ChangeFavoriteAsync(session, add: true);

    [ExportAIFunction("remove_favorite")]
    [Description("Remove a session from the user's saved favourites in the current city.")]
    public Task<string> RemoveFavoriteAsync(
        [Description("Session ID or title.")] string session,
        CancellationToken cancellationToken = default) =>
        ChangeFavoriteAsync(session, add: false);

    private async Task<string> ChangeFavoriteAsync(string session, bool add)
    {
        var city = await preferences.GetSelectedCityAsync(database);
        var sessions = await database.GetSessionsAsync(city.Id);
        var match = sessions.FirstOrDefault(s =>
            s.Id.Equals(session, StringComparison.OrdinalIgnoreCase) ||
            s.Title.Contains(session, StringComparison.OrdinalIgnoreCase) ||
            session.Contains(s.Title, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return $"No session matching '{session}' in {city.Name}.";

        var action = add ? "add_favorite" : "remove_favorite";
        preferences.SetFavorite(match.Id, add);
        logger.LogInformation("[AI Tool] {Action}('{SessionId}') saved.", action, match.Id);

        return add
            ? $"Added '{match.Title}' to favourites."
            : $"Removed '{match.Title}' from favourites.";
    }
}
