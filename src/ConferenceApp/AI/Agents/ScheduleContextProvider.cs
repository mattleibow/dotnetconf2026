using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Agents.AI;

namespace ConferenceApp.AI;

/// <summary>
/// Context Provider 2: Injects the full day schedule of times, rooms, topics, and titles for the current city.
/// </summary>
public sealed class ScheduleContextProvider(IServiceProvider services) : AIContextProvider
{
    private readonly ConferenceDbProvider dbProvider = services.GetRequiredService<ConferenceDbProvider>();
    private readonly UserPreferencesService preferences = services.GetRequiredService<UserPreferencesService>();

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var db = await dbProvider.GetDbAsync();
        var city = await preferences.GetSelectedCityAsync();
        var sessions = db.Sessions.Where(s => s.CityId == city.Id);

        var lines = sessions.Select(session =>
        {
            var tag = !string.IsNullOrWhiteSpace(session.Topic) ? $" [{session.Topic}]" : "";
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
