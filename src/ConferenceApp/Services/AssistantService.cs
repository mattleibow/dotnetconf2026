using System.Text;
using ConferenceApp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
#if IOS || MACCATALYST
using Microsoft.Maui.Essentials.AI;
#endif

namespace ConferenceApp.Services;

public sealed class AssistantService(ConferenceService conferenceService, IServiceProvider services)
{
    public static IChatClient AddSessionContext(IChatClient client, IServiceProvider services)
    {
        var search = services.GetRequiredService<SessionSearchService>();
        var conference = services.GetRequiredService<ConferenceService>();
        var logger = services.GetRequiredService<ILogger<AssistantService>>();

        return new ChatClientBuilder(client)
            .Use(async (messages, options, next, cancellationToken) =>
            {
                var request = messages.ToList();
                var questionIndex = request.FindLastIndex(message => message.Role == ChatRole.User);
                if (questionIndex >= 0 && !string.IsNullOrWhiteSpace(request[questionIndex].Text))
                {
                    var previousAnswer = request.Take(questionIndex).LastOrDefault(message => message.Role == ChatRole.Assistant)?.Text;
                    var query = string.Join(" ", new[] { request[questionIndex].Text, previousAnswer }.Where(text => !string.IsNullOrWhiteSpace(text)));
                    var results = await search.SearchSessionsAsync(query);
                    cancellationToken.ThrowIfCancellationRequested();
                    var data = await conference.GetConferenceAsync();
                    var context = new StringBuilder("Relevant session details:\n");
                    var sessions = results.Results.Where(result => result.Session.Kind == "session").Take(3).ToArray();
                    foreach (var result in sessions)
                    {
                        var session = result.Session;
                        var speakers = string.Join(", ", data.Speakers.Where(s => session.SpeakerIds.Contains(s.Id)).Select(s => s.Name));
                        var abstractText = await conference.GetContentAsync(session.Content);
                        cancellationToken.ThrowIfCancellationRequested();
                        context.AppendLine($"{session.Title} | {session.Start:yyyy-MM-dd HH:mm}-{session.End:HH:mm} | {speakers}\n{abstractText}\n");
                        logger.LogInformation("Assistant retrieval added session {SessionId}: {Title}", session.Id, session.Title);
                    }
                    if (!string.IsNullOrEmpty(results.Warning))
                        logger.LogWarning("Assistant retrieval: {Warning}", results.Warning);
                    if (sessions.Length > 0)
                        request.Insert(questionIndex, new ChatMessage(ChatRole.User, context.ToString()));
                }
                await next(request, options, cancellationToken);
            })
            .Build(services);
    }

    public async Task<bool> IsAvailableAsync()
    {
#if IOS || MACCATALYST
        if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
        {
#pragma warning disable MAUIAI0001
            return (await AppleIntelligenceChatClient.GetModelStatusAsync()).Availability == ChatModelAvailability.Available;
#pragma warning restore MAUIAI0001
        }
#endif
        return false;
    }

    public IChatClient? Client => services.GetService<IChatClient>();

    public async Task<(string Instructions, string ConversationKey)> BuildInstructionsAsync(TimeSpan? eventTime = null)
    {
        var data = await conferenceService.GetConferenceAsync();
        var city = data.Cities.First(c => c.Id == conferenceService.CityId);
        var venue = data.Venues.First(v => v.Id == city.VenueId);
        var sessions = data.Sessions.Where(s => s.CityId == city.Id).OrderBy(s => s.Start).ThenBy(s => s.Track).ToArray();
        var offset = sessions.FirstOrDefault()?.Start.Offset ?? TimeSpan.FromHours(2);
        var now = DateTimeOffset.UtcNow.ToOffset(offset);
        if (eventTime is not null)
            now = new DateTimeOffset(city.Date.ToDateTime(TimeOnly.MinValue).Add(eventTime.Value), offset);
        var upcoming = sessions.Where(s => s.Start > now).ToArray();
        var next = upcoming.Length == 0 ? "None" :
            $"{upcoming[0].Start:yyyy-MM-dd HH:mm}: {string.Join("; ", upcoming.TakeWhile(s => s.Start == upcoming[0].Start).Select(s => s.Title))}";

        var text = new StringBuilder();
        text.AppendLine($"Event: {data.Name}; city: {city.Name}; date: {city.Date:yyyy-MM-dd}; venue: {venue.Name}, {venue.Address}.");
        var favorites = sessions.Where(s => conferenceService.IsFavorite(s.Id)).ToArray();
        text.AppendLine($"Favourites: {string.Join("; ", favorites.Select(s => s.Title).DefaultIfEmpty("none"))}.");
        text.AppendLine("Agenda (start-end | track | title | speakers | topic | summary):");
        foreach (var session in sessions)
        {
            var speakers = string.Join(", ", session.SpeakerIds.Select(id => data.Speakers.FirstOrDefault(s => s.Id == id)?.Name).Where(name => name is not null));
            text.AppendLine($"{session.Start:HH:mm}-{session.End:HH:mm} | {Compact(session.Track)} | {Compact(session.Title)} | {Compact(speakers)} | {Compact(session.Topic)} | {Compact(session.Summary)}");
        }
        var dataText = text.ToString();
        var instructions = "You are a conference assistant. Use only the data below; if the answer is not there, say you don't know. Never invent speaker backgrounds, sessions, times, or details. " +
            "Each agenda line describes one event; copy its title, time, track and speakers from that same line. Never combine details from different lines. " +
            "When asked about a speaker's talk, include every matching session, including keynotes. For broad questions, give a few relevant examples, not an exhaustive list. Be concise.\n" +
            $"Current local time: {now:yyyy-MM-dd HH:mm zzz}. Agenda times are local. 'Next' means starting after the current time. Next scheduled start: {next}.\n" + dataText;
        return (instructions, $"{city.Id}\n{string.Join("\n", favorites.Select(s => s.Id))}");
    }

    private static string Compact(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
