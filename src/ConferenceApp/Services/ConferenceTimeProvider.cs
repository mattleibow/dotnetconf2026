namespace ConferenceApp.Services;

// Provides the current local time, or a pinned time of day when rehearsing demos outside conference hours.
public class ConferenceTimeProvider(TimeOnly? fixedTime = null) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        var now = System.GetLocalNow();
        return fixedTime is { } time
            ? new DateTimeOffset(DateOnly.FromDateTime(now.DateTime), time, now.Offset).ToUniversalTime()
            : now.ToUniversalTime();
    }
}
