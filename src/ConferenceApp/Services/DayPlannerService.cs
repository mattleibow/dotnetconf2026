using System.Text.Json;
using System.Text.RegularExpressions;
using ConferenceApp.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
#if IOS || MACCATALYST
using Microsoft.Maui.Essentials.AI;
#endif

namespace ConferenceApp.Services;

public sealed class DayPlannerService(SessionSearchService search, ConferenceService conference, IServiceProvider services)
{
    public async Task<DayPlan> PlanAsync(string request, string cityId, CancellationToken cancellationToken)
    {
#if IOS || MACCATALYST
#if IOS
        if (!OperatingSystem.IsIOSVersionAtLeast(26))
#else
        if (!OperatingSystem.IsMacCatalystVersionAtLeast(26))
#endif
            throw new PlatformNotSupportedException("On-device planning requires Apple Intelligence.");
#pragma warning disable MAUIAI0001
        var client = services.GetRequiredService<AppleIntelligenceChatClient>();
#pragma warning restore MAUIAI0001
        var preferencesAgent = new ChatClientAgent(client, """
            Extract conference preferences from the user's request. Return only JSON with
            "interests": an array of short, broad search terms (for example "mobile" and
            "AI"), and "leaveBy": a 24-hour HH:mm
            string or null. Include the user's profession as an interest. Interpret an
            afternoon departure such as "leaving at 3" as 15:00. Do not invent interests.
            """, name: "preferences");
        var reasonAgent = new ChatClientAgent(client, """
            Write exactly one short sentence explaining why this talk fits the interests.
            Use only the supplied talk and preferences. Do not claim a talk is about mobile
            development unless its description says so. Avoid formulaic openings such as
            "This talk fits because". Do not invent facts, introduce yourself, add a
            heading, or write anything else.
            """, name: "fit");

        async ValueTask<PlanPreferences> ExtractAsync(string input, CancellationToken token)
        {
            var response = await preferencesAgent.RunAsync(input, cancellationToken: token);
            var text = response.Text.Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                var firstLine = text.IndexOf('\n');
                var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (firstLine >= 0 && lastFence > firstLine)
                    text = text[(firstLine + 1)..lastFence].Trim();
            }
            var preferences = JsonSerializer.Deserialize<PlanPreferences>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("The preferences agent returned no preferences.");
            if (preferences.Interests is null || preferences.Interests.All(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("The preferences agent did not identify any interests.");
            var leaveBy = ExplicitDeparture(input) ?? preferences.LeaveBy;
            if (leaveBy is not null && !TimeOnly.TryParseExact(leaveBy, "HH:mm", out _))
                throw new InvalidDataException("The preferences agent returned an invalid departure time.");
            return preferences with { LeaveBy = leaveBy };
        }

        async ValueTask<Candidates> SearchAsync(PlanPreferences preferences, CancellationToken token)
        {
            var results = new List<SearchHit>();
            var warnings = new HashSet<string>();
            foreach (var interest in preferences.Interests.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                var response = await search.SearchSessionsAsync(interest, cityId);
                if (!string.IsNullOrWhiteSpace(response.Warning))
                    warnings.Add(response.Warning);
                var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(interest.Trim())}(?![\p{{L}}\p{{N}}])";
                results.AddRange(response.Results.Where(r => r.Session.Kind == "session" && !r.IsSemantic &&
                    Regex.IsMatch(r.MatchText, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    .Select(r => new SearchHit(r.Session, interest)));
            }
            return new Candidates(preferences, results, string.Join(" ", warnings));
        }

        ValueTask<Selection> SelectAsync(Candidates candidates, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var departure = candidates.Preferences.LeaveBy is null ? (TimeOnly?)null :
                TimeOnly.ParseExact(candidates.Preferences.LeaveBy, "HH:mm");
            var ranked = candidates.Hits.GroupBy(hit => hit.Session.Id)
                .Select(g => new SelectedTalk(g.First().Session, g.Select(hit => hit.Interest).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
                .Where(x => departure is null || TimeOnly.FromDateTime(x.Session.End.DateTime) <= departure)
                .OrderByDescending(x => x.Interests.Length).ThenBy(x => x.Session.Start).ThenBy(x => x.Session.Id);
            var selected = new List<SelectedTalk>();
            foreach (var item in ranked)
            {
                if (selected.All(s => item.Session.End <= s.Session.Start || item.Session.Start >= s.Session.End))
                    selected.Add(item);
            }
            return ValueTask.FromResult(new Selection(candidates.Preferences, selected.OrderBy(s => s.Session.Start).ToList(), candidates.Warning));
        }

        async ValueTask<DayPlan> ExplainAsync(Selection selection, CancellationToken token)
        {
            var talks = await Task.WhenAll(selection.Talks.Select(async talk =>
            {
                var session = talk.Session;
                var abstractText = await conference.GetContentAsync(session.Content);
                token.ThrowIfCancellationRequested();
                var response = await reasonAgent.RunAsync(
                    $"Matching interests: {string.Join(", ", talk.Interests)}\nTalk: {session.Title}\nDescription: {abstractText}",
                    cancellationToken: token);
                var reason = response.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
                if (string.IsNullOrWhiteSpace(reason))
                    throw new InvalidDataException($"No explanation was returned for {session.Title}.");
                return new PlannedTalk(session, reason);
            }));
            return new DayPlan(cityId, talks, selection.Warning, selection.Preferences.Interests, selection.Preferences.LeaveBy);
        }

        var extract = ((Func<string, CancellationToken, ValueTask<PlanPreferences>>)ExtractAsync).BindAsExecutor("preferences");
        var find = ((Func<PlanPreferences, CancellationToken, ValueTask<Candidates>>)SearchAsync).BindAsExecutor("search");
        var pick = ((Func<Candidates, CancellationToken, ValueTask<Selection>>)SelectAsync).BindAsExecutor("pick");
        var explain = ((Func<Selection, CancellationToken, ValueTask<DayPlan>>)ExplainAsync).BindAsExecutor("explain");
        var workflow = new WorkflowBuilder(extract).AddEdge(extract, find).AddEdge(find, pick)
            .AddEdge(pick, explain).WithOutputFrom(explain).Build();
        var run = await InProcessExecution.RunAsync(workflow, request, cancellationToken: cancellationToken);
        return run.OutgoingEvents.OfType<WorkflowOutputEvent>().Select(e => e.As<DayPlan>()).Single()
            ?? throw new InvalidDataException("The planning workflow returned no plan.");
#else
        _ = search;
        _ = conference;
        _ = services;
        throw new PlatformNotSupportedException("On-device planning requires Apple Intelligence.");
#endif
    }

    private sealed record PlanPreferences(string[] Interests, string? LeaveBy);
    private sealed record SearchHit(Session Session, string Interest);
    private sealed record SelectedTalk(Session Session, string[] Interests);
    private sealed record Candidates(PlanPreferences Preferences, List<SearchHit> Hits, string Warning);
    private sealed record Selection(PlanPreferences Preferences, List<SelectedTalk> Talks, string Warning);

    private static string? ExplicitDeparture(string request)
    {
        var match = Regex.Match(request,
            @"\b(?:leav(?:e|es|ing)|depart(?:ing|ure)?|until)\s*(?:at|by|before|around)?\s*(\d{1,2})(?::(\d{2}))?\s*(a\.?m\.?|p\.?m\.?)?(?![\w:])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;
        var hour = int.Parse(match.Groups[1].Value);
        var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        var meridiem = match.Groups[3].Value.Replace(".", "", StringComparison.Ordinal).ToLowerInvariant();
        if (minute > 59 || hour > 23 || (meridiem.Length > 0 && (hour < 1 || hour > 12)))
            throw new InvalidDataException("The departure time is invalid.");
        if (meridiem == "pm" && hour < 12)
            hour += 12;
        else if (meridiem == "am" && hour == 12)
            hour = 0;
        else if (meridiem.Length == 0 && hour is >= 1 and <= 7)
            hour += 12;
        return $"{hour:00}:{minute:00}";
    }
}

public sealed record PlannedTalk(Session Session, string Reason);
public sealed record DayPlan(string CityId, IReadOnlyList<PlannedTalk> Talks, string Warning, string[] Interests, string? LeaveBy);
