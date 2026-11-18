using System.ComponentModel;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.AI.Attributes;

namespace ConferenceApp.AI;

// Source-generated AOT-safe tool context exposing NavigationTools as AITool instances.
[AIToolSource(typeof(NavigationTools))]
public partial class NavigationToolContext : AIToolContext;

// Tools for navigating to a session page, speaker profile page, or top-level app tab.
public sealed class NavigationTools(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    IDispatcher dispatcher,
    ILogger<NavigationTools> logger)
{
    [ExportAIFunction("navigate_to_session")]
    [Description("Open and navigate to a specific session's detail page in the app.")]
    public async Task<string> NavigateToSessionAsync(
        [Description("Session ID, title, or topic.")] string session,
        [FromServices] SearchService search,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var match = db.FindSession(city.Id, session);

        if (match is null)
        {
            var searched = await search.SearchSessionsAsync(city.Id, session);
            match = searched.FirstOrDefault(s => s.IsTalk);
        }

        if (match is null)
            return $"No session matching '{session}' in {city.Name}.";

        logger.LogInformation("[AI Tool] navigate_to_session('{SessionId}')", match.Id);
        await dispatcher.DispatchAsync(() => Shell.Current.GoToAsync($"session?id={match.Id}"));
        return $"Navigated to session '{match.Title}'.";
    }

    [ExportAIFunction("navigate_to_speaker")]
    [Description("Open and navigate to a speaker's profile page in the app.")]
    public async Task<string> NavigateToSpeakerAsync(
        [Description("Speaker ID or name.")] string speaker,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var trimmed = speaker.Trim();

        var match = db.Speakers.FirstOrDefault(s =>
            s.CityIds.Contains(city.Id) && (
                s.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(s.Name, StringComparison.OrdinalIgnoreCase)))
            ?? db.Speakers.FirstOrDefault(s =>
                s.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(s.Name, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return $"No speaker matching '{speaker}'.";

        logger.LogInformation("[AI Tool] navigate_to_speaker('{SpeakerId}')", match.Id);
        await dispatcher.DispatchAsync(() => Shell.Current.GoToAsync($"speaker?id={match.Id}"));
        return $"Navigated to speaker profile for '{match.Name}'.";
    }

    [ExportAIFunction("navigate_to_page")]
    [Description("Navigate to a top-level app tab ('agenda', 'event', 'speakers') or switch the active city ('Durban', 'Cape Town', 'Bloemfontein').")]
    public async Task<string> NavigateToPageAsync(
        [Description("Tab name ('agenda', 'event', 'speakers') or city name ('Durban', 'Cape Town', 'Bloemfontein').")] string page,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var trimmed = page.Trim();

        if (db.Cities.FirstOrDefault(c =>
            c.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) is { } city)
        {
            logger.LogInformation("[AI Tool] navigate_to_page(city: '{CityId}')", city.Id);
            await dispatcher.DispatchAsync(() => preferences.SelectedCityId = city.Id);
            return $"Switched active city to {city.Name}.";
        }

        var route = trimmed.ToLowerInvariant() switch
        {
            var s when s.Contains("speaker") => "//speakers",
            var s when s.Contains("event") || s.Contains("venue") || s.Contains("map") => "//event",
            _ => "//agenda"
        };

        logger.LogInformation("[AI Tool] navigate_to_page(route: '{Route}')", route);
        await dispatcher.DispatchAsync(() => Shell.Current.GoToAsync(route));
        return $"Navigated to {route.TrimStart('/')}.";
    }
}
