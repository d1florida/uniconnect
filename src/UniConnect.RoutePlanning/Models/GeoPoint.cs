namespace UniConnect.RoutePlanning.Models;

public readonly record struct GeoPoint(decimal Latitude, decimal Longitude)
{
    public double LatRad => (double)Latitude * Math.PI / 180.0;
    public double LngRad => (double)Longitude * Math.PI / 180.0;
}
