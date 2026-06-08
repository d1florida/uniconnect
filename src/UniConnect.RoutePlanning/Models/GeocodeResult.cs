namespace UniConnect.RoutePlanning.Models;

public readonly record struct GeocodeResult(decimal Latitude, decimal Longitude, string? FormattedAddress = null)
{
    public GeoPoint ToPoint() => new(Latitude, Longitude);
}
