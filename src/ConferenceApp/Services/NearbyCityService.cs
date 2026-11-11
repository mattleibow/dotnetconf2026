namespace ConferenceApp.Services;

public sealed class NearbyCityService
{
    public async Task<string?> FindNearestAsync()
    {
        var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
            permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
            return null;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var location = await Geolocation.Default.GetLocationAsync(
            new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(15)), timeout.Token);
        if (location is null)
            return null;
        var cities = new[]
        {
            (Id: "durban", Latitude: -29.8587, Longitude: 31.0218),
            (Id: "cape-town", Latitude: -33.9249, Longitude: 18.4241),
            (Id: "bloemfontein", Latitude: -29.0852, Longitude: 26.1596)
        };
        return cities.MinBy(c => Location.CalculateDistance(location.Latitude, location.Longitude,
            c.Latitude, c.Longitude, DistanceUnits.Kilometers)).Id;
    }
}
