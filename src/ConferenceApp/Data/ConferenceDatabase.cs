namespace ConferenceApp.Data;

public class ConferenceDatabase(
    string name,
    string timeZone,
    IReadOnlyList<City> cities,
    IReadOnlyList<Session> sessions,
    IReadOnlyList<Speaker> speakers,
    IReadOnlyList<Venue> venues,
    IReadOnlyList<Sponsor> sponsors)
{
    public string Name { get; } = name;
    public string TimeZone { get; } = timeZone;
    public IReadOnlyList<City> Cities { get; } = cities;
    public IReadOnlyList<Session> Sessions { get; } = sessions;
    public IReadOnlyList<Speaker> Speakers { get; } = speakers;
    public IReadOnlyList<Venue> Venues { get; } = venues;
    public IReadOnlyList<Sponsor> Sponsors { get; } = sponsors;

    public City GetCity(string? cityId = null)
    {
        if (!string.IsNullOrEmpty(cityId) && Cities.FirstOrDefault(c => c.Id == cityId) is { } match)
            return match;

        var today = DateOnly.FromDateTime(DateTime.Now);
        return Cities.FirstOrDefault(c => c.Date >= today) ?? Cities[^1];
    }
}
