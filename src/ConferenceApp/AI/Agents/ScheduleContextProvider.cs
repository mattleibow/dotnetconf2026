using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Agents.AI;

namespace ConferenceApp.AI;

// Injects the full day schedule of times, rooms, topics, and titles for the current city.
public sealed class ScheduleContextProvider(
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences) : AIContextProvider
{
    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var sessions = db.Sessions.Where(s => s.CityId == city.Id);

        var lines = sessions.Select(session =>
        {
            var tag = session.Topics.Count > 0 ? $" [{string.Join(", ", session.Topics)}]" : "";
            var byline = !string.IsNullOrWhiteSpace(session.SpeakerNames) ? $" ({session.SpeakerNames})" : "";
            return $"- {session.Time} | {session.Track ?? "All attendees"}{tag} | {session.Title}{byline}";
        });

        return new AIContext
        {
            Instructions = $"""
                FULL DAY SCHEDULE FOR {city.Name.ToUpperInvariant()}
                {string.Join('\n', lines)}
                """
        };
    }
}
