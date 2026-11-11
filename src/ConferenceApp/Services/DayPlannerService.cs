using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConferenceApp.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ConferenceApp.Services;

public sealed class DayPlannerService(SessionSearchService search, ConferenceService data, CityService city, IServiceProvider services)
{
    public async Task<DayPlan> PlanAsync(string request, CancellationToken cancellationToken)
    {
        var cityId = city.SelectedId;
        var client = services.GetRequiredService<IChatClient>();
        var preferences = new PreferencesExecutor(client);
        var candidates = new CandidatesExecutor(search, city, cityId);
        var selection = new SelectionExecutor();
        var reasons = new ReasonsExecutor(client, data);
        var workflow = new WorkflowBuilder(preferences)
            .AddEdge(preferences, candidates)
            .AddEdge(candidates, selection)
            .AddEdge(selection, reasons)
            .WithOutputFrom(reasons)
            .Build();
        var run = await InProcessExecution.RunAsync(workflow, request, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (run.OutgoingEvents.OfType<WorkflowErrorEvent>().FirstOrDefault() is { } error)
            throw new InvalidOperationException("The planning workflow failed.", error.Exception);
        if (city.SelectedId != cityId)
            throw new InvalidOperationException("The selected city changed while planning.");
        var plan = run.OutgoingEvents.OfType<WorkflowOutputEvent>().Select(e => e.Data).OfType<DayPlan>().Single();
        return plan with { CityId = cityId };
    }
}

public sealed record DayPreferences(string[] Interests, string LeaveBy);
public sealed record DayPlanItem(Session Session, string Reason);
public sealed record DayPlan(string CityId, DayPreferences Preferences, IReadOnlyList<DayPlanItem> Items);
internal sealed record CandidateSet(DayPreferences Preferences, IReadOnlyList<Session> Sessions);
internal sealed record Selection(DayPreferences Preferences, IReadOnlyList<Session> Sessions);

[SendsMessage(typeof(DayPreferences))]
internal sealed partial class PreferencesExecutor(IChatClient client) : Executor<string>("preferences")
{
    public override async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var agent = client.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "preferences",
            ChatOptions = new ChatOptions
            {
                Instructions = "Extract the user's technical interests as short search terms and their departure time in local 24-hour HH:mm format. Interpret an unqualified afternoon departure such as 'leaving at 3' as 15:00. If no departure time is specified, use an empty string. Return only the requested JSON.",
                ResponseFormat = ChatResponseFormat.ForJsonSchema<DayPreferences>()
            }
        });
        var response = await agent.RunAsync(message, cancellationToken: cancellationToken);
        var preferences = JsonSerializer.Deserialize<DayPreferences>(response.Text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The model returned no preferences.");
        if (preferences.Interests is not { Length: > 0 } || preferences.Interests.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("The model did not identify any interests.");
        if (preferences.LeaveBy is null || (preferences.LeaveBy.Length > 0 && !TimeOnly.TryParseExact(preferences.LeaveBy, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            throw new InvalidOperationException("The model returned an invalid departure time.");
        await context.SendMessageAsync(preferences, cancellationToken: cancellationToken);
    }
}

[SendsMessage(typeof(CandidateSet))]
internal sealed partial class CandidatesExecutor(SessionSearchService search, CityService city, string cityId) : Executor<DayPreferences>("candidates")
{
    public override async ValueTask HandleAsync(DayPreferences message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var scores = new Dictionary<string, (Session Session, int Score)>();
        foreach (var interest in message.Interests.Distinct(StringComparer.OrdinalIgnoreCase).Take(8))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (city.SelectedId != cityId)
                throw new InvalidOperationException("The selected city changed while planning.");
            var results = await search.SearchSessionsAsync(interest);
            foreach (var (result, index) in results.Select((result, index) => (result, index)))
            {
                if (result.Session.CityId != cityId || result.Session.Kind == "general")
                    continue;
                var weight = Math.Max(1, 10 - index);
                scores.TryGetValue(result.Session.Id, out var previous);
                scores[result.Session.Id] = (result.Session, previous.Score + weight);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        await context.SendMessageAsync(new CandidateSet(message, scores.Values.OrderByDescending(x => x.Score)
            .ThenBy(x => x.Session.Start).Select(x => x.Session).ToList()), cancellationToken: cancellationToken);
    }
}

[SendsMessage(typeof(Selection))]
internal sealed partial class SelectionExecutor() : Executor<CandidateSet>("selection")
{
    public override async ValueTask HandleAsync(CandidateSet message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var leaveBy = message.Preferences.LeaveBy.Length == 0 ? (TimeOnly?)null :
            TimeOnly.ParseExact(message.Preferences.LeaveBy, "HH:mm", CultureInfo.InvariantCulture);
        var picked = new List<Session>();
        foreach (var session in message.Sessions)
        {
            if (leaveBy is not null && TimeOnly.FromDateTime(session.End.DateTime) > leaveBy.Value)
                continue;
            if (picked.Any(other => session.Start < other.End && other.Start < session.End))
                continue;
            picked.Add(session);
        }
        await context.SendMessageAsync(new Selection(message.Preferences, picked.OrderBy(s => s.Start).ToList()), cancellationToken: cancellationToken);
    }
}

[YieldsOutput(typeof(DayPlan))]
internal sealed partial class ReasonsExecutor(IChatClient client, ConferenceService data) : Executor<Selection>("reasons")
{
    public override async ValueTask HandleAsync(Selection message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var agent = client.AsAIAgent(instructions: "Write one short, factual sentence explaining how this talk relates to the user's interests. Use only the supplied session details. Do not claim the talk covers a technology or audience not mentioned in its description.", name: "fit-explanation");
        var items = await Task.WhenAll(message.Sessions.Select(async session =>
        {
            var content = await data.GetContentAsync(session.Content);
            cancellationToken.ThrowIfCancellationRequested();
            var response = await agent.RunAsync($"Interests: {string.Join(", ", message.Preferences.Interests)}\nTalk: {session.Title}\nDescription: {content}", cancellationToken: cancellationToken);
            var reason = string.Join(" ", response.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (reason.Length == 0)
                throw new InvalidOperationException("The model returned an empty explanation.");
            reason = Regex.Split(reason, @"(?<=[.!?])\s+(?=\p{Lu})", RegexOptions.CultureInvariant)[0];
            return new DayPlanItem(session, reason);
        }));
        await context.YieldOutputAsync(new DayPlan("", message.Preferences, items), cancellationToken);
    }
}
