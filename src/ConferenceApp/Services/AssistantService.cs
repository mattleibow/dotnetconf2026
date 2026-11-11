using System.Text;
using ConferenceApp.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
#if IOS || MACCATALYST
using Microsoft.Maui.Essentials.AI;
#endif

namespace ConferenceApp.Services;

public sealed class AssistantService(ConferenceService data, CityService city, FavoritesService favorites, SessionSearchService search, IServiceProvider services)
{
    public string SelectedCityId => city.SelectedId;
    public async Task<string> GetCityLabelAsync()
    {
        var selected = (await data.GetConferenceAsync()).Cities.First(c => c.Id == city.SelectedId);
        return $"{selected.Name} · {selected.Date:d MMM yyyy}";
    }
#if IOS || MACCATALYST
    private IChatClient? client;
#endif

    public async Task<string?> GetUnavailableReasonAsync()
    {
#if IOS || MACCATALYST
        if (!(OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26)))
            return "The assistant needs iOS or Mac Catalyst 26 or later.";
#pragma warning disable MAUIAI0001
        var status = await AppleIntelligenceChatClient.GetModelStatusAsync();
        if (status.Availability != ChatModelAvailability.Available)
            return "The on-device model isn't ready. Check Apple Intelligence in your device settings and try again.";
        client ??= new ChatClientBuilder(services.GetRequiredService<IChatClient>())
            .ConfigureOptions(options => options.Tools = [.. AssistantTools.Default.Tools])
            .Use(async (messages, options, next, cancellationToken) =>
            {
                var prompt = messages.ToList();
                var questionIndex = prompt.FindLastIndex(message => message.Role == ChatRole.User);
                if (questionIndex >= 0)
                {
                    var question = prompt[questionIndex].Text;
                    var previousAnswer = prompt.Take(questionIndex).LastOrDefault(message => message.Role == ChatRole.Assistant)?.Text;
                    var results = await search.SearchSessionsAsync($"{question} {previousAnswer}".Trim());
                    cancellationToken.ThrowIfCancellationRequested();
                    var conference = await data.GetConferenceAsync();
                    var context = new StringBuilder("Relevant session details:\n");
                    foreach (var result in results.Take(3))
                    {
                        var session = result.Session;
                        var speakers = string.Join(", ", conference.Speakers.Where(s => session.SpeakerIds.Contains(s.Id)).Select(s => s.Name));
                        var abstractText = await data.GetContentAsync(session.Content);
                        context.AppendLine($"{session.Title} | {session.Start:HH:mm}-{session.End:HH:mm} | {speakers}\n{abstractText}\n");
                    }
                    prompt.Insert(questionIndex, new ChatMessage(ChatRole.User, context.ToString()));
                }
                await next(prompt, options, cancellationToken);
            })
            .Build();
#pragma warning restore MAUIAI0001
        return null;
#else
        _ = services;
        _ = search;
        await Task.CompletedTask;
        return "On-device chat isn't available on this platform yet. Your conference data stays on this device.";
#endif
    }

    public async Task<string> BuildInstructionsAsync(TimeSpan? simulatedTime = null)
    {
        var conference = await data.GetConferenceAsync();
        var selected = conference.Cities.First(c => c.Id == city.SelectedId);
        var venue = conference.Venues.First(v => v.Id == selected.VenueId);
        var sessions = conference.Sessions.Where(s => s.CityId == selected.Id).OrderBy(s => s.Start).ToList();
        var offset = sessions.Count > 0 ? sessions[0].Start.Offset : TimeSpan.Zero;
        var now = simulatedTime is { } time
            ? new DateTimeOffset(selected.Date.ToDateTime(TimeOnly.MinValue).Add(time), offset)
            : DateTimeOffset.UtcNow.ToOffset(offset);
        var speakerNames = conference.Speakers.ToDictionary(s => s.Id, s => s.Name);
        var result = new StringBuilder();
        result.AppendLine("You are a conference assistant. Answer only from the current conference data below or the available tools, never from prior knowledge or earlier answers. Use tools to look up speaker biographies, sponsors, venues and session details. For a time range, use the time range tool and list every matching session, including all tracks. If the answer is missing, say you don't know. Copy session titles and times exactly. Times are local to the event. Be concise.");
        result.AppendLine($"Event: {conference.Name}; {selected.Name}; {selected.Date:yyyy-MM-dd}; {venue.Name}, {venue.Address}.");
        result.AppendLine($"Current local time: {now:yyyy-MM-dd HH:mm zzz}.");
        var saved = conference.Sessions.Where(s => s.CityId == selected.Id && favorites.Contains(s.Id))
            .Select(s => $"{Compact(s.Title, 160)} ({s.CityId}, {s.Start:dd MMM HH:mm})");
        result.AppendLine($"Favourites: {string.Join("; ", saved)}");
        result.AppendLine("Agenda: start-end | track | title | speakers | topic | summary. A * marks a favourite.");
        foreach (var session in sessions)
        {
            var speakers = string.Join(", ", session.SpeakerIds.Where(speakerNames.ContainsKey).Select(id => speakerNames[id]));
            result.Append(favorites.Contains(session.Id) ? "* " : "- ");
            result.AppendLine($"{session.Start:HH:mm}-{session.End:HH:mm} | {Compact(session.Track, 40)} | {Compact(session.Title, 160)} | {Compact(speakers, 100)} | {Compact(session.Topic, 30)} | {Compact(session.Summary, 120)}");
        }
        return result.ToString();
    }

    private static string Compact(string? value, int maxLength)
    {
        var text = string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
    }

    public async Task<IReadOnlyList<Session>> FindMentionedSessionsAsync(string answer, string cityId)
    {
        var conference = await data.GetConferenceAsync();
        var normalizedAnswer = NormalizeQuotes(answer);
        return conference.Sessions.Where(s => s.CityId == cityId && s.Kind != "general" &&
            normalizedAnswer.Contains(NormalizeQuotes(s.Title), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static string NormalizeQuotes(string text) => text
        .Replace('‘', '\'').Replace('’', '\'')
        .Replace('“', '"').Replace('”', '"');

    public IAsyncEnumerable<ChatResponseUpdate> StreamAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
#if IOS || MACCATALYST
        return (client ?? throw new InvalidOperationException("The on-device model is unavailable."))
            .GetStreamingResponseAsync(messages, cancellationToken: cancellationToken);
#else
        throw new PlatformNotSupportedException("On-device chat is unavailable on this platform.");
#endif
    }
}
