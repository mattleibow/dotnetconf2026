using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Agents.AI;

namespace ConferenceApp.AI;

/// <summary>
/// Injects candidate talks in the current city that are not yet saved in the attendee's favourites.
/// </summary>
public sealed class AvailableSessionsContextProvider(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences) : AIContextProvider
{
    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var sessions = db.Sessions.Where(s =>
            s.CityId == city.Id &&
            s.IsTalk &&
            !preferences.IsFavorite(s.Id));

        var lines = sessions.Select(session =>
        {
            var tag = session.Topics.Count > 0 ? $" [{string.Join(", ", session.Topics)}]" : "";
            var byline = !string.IsNullOrWhiteSpace(session.SpeakerNames) ? $" ({session.SpeakerNames})" : "";
            var summary = !string.IsNullOrWhiteSpace(session.Summary) ? $" — {session.Summary}" : "";
            return $"- {session.Time} | {session.Track ?? "All attendees"}{tag} | {session.Title}{byline}{summary}";
        });

        return new AIContext
        {
            Instructions = $"""
                CANDIDATE SESSIONS IN {city.Name.ToUpperInvariant()} (EXCLUDING ALREADY-FAVOURITED TALKS)
                {string.Join('\n', lines)}
                """
        };
    }
}
