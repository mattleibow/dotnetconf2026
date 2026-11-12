using System.Globalization;

namespace ConferenceApp.Services;

internal static class ConferenceTime
{
    public static TimeOnly ParseDaytime(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        var hasPeriod = text.EndsWith("am") || text.EndsWith("pm");
        TimeOnly time;
        if (hasPeriod)
        {
            if (!TimeOnly.TryParseExact(text, ["h tt", "htt", "h:mm tt", "h:mmtt"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                throw new ArgumentException("Use a daytime hour or an HH:mm time.");
        }
        else
        {
            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) && hour is >= 1 and <= 23)
                time = new(hour, 0);
            else if (!TimeOnly.TryParseExact(text, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                throw new ArgumentException("Use a daytime hour or an HH:mm time.");
            if (time.Hour is >= 1 and <= 7)
                time = time.AddHours(12);
        }
        if (time.Hour < 8)
            throw new ArgumentException("Use a daytime range starting at 08:00 or later.");
        return time;
    }
}
