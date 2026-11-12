using System.Text.Json.Serialization;

namespace ConferenceApp.Models;

public record DayPreferences(string Interests, string StartTime, string EndTime);

public record DayRecommendation(Session Session, string Reason);

public record DayPlan(City City, DayPreferences Preferences, TimeOnly Start, TimeOnly End,
    IReadOnlyList<DayRecommendation> Sessions);

[JsonSerializable(typeof(DayPreferences))]
internal partial class DayPlanJsonContext : JsonSerializerContext;
