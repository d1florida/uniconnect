using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Routing;

public static class GeoMath
{
    private const double EarthRadiusKm = 6371.0;

    public static double HaversineKilometers(GeoPoint from, GeoPoint to)
    {
        var dLat = to.LatRad - from.LatRad;
        var dLng = to.LngRad - from.LngRad;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(from.LatRad) * Math.Cos(to.LatRad) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public static GeoPoint DeterministicGeocode(string normalizedAddress, AddressGeocodeHints hints = default)
    {
        var hash = StableHash(normalizedAddress);
        var anchor = TryPostalCentroid(hints.PostalCode) ?? new GeoPoint(39.8283m, -98.5795m);
        var lat = (double)anchor.Latitude + (hash % 1000) / 10000.0;
        var lng = (double)anchor.Longitude + ((hash / 1000) % 1000) / 10000.0;
        return new GeoPoint((decimal)lat, (decimal)lng);
    }

    public static GeoPoint? TryPostalCentroid(string? postalCode) =>
        postalCode switch
        {
            "33773" => new GeoPoint(27.8828m, -82.7627m),
            "94103" => new GeoPoint(37.7725m, -122.4140m),
            "94105" => new GeoPoint(37.7897m, -122.3942m),
            _ => null,
        };

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var c in value)
                hash = (hash * 31) + char.ToLowerInvariant(c);
            return Math.Abs(hash);
        }
    }
}
