using Microsoft.Extensions.Options;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.RoutePlanning.Options;
using UniConnect.RoutePlanning.Routing;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public sealed class HaversineTravelMatrix(IOptions<RoutePlanningOptions> options) : ITravelTimeMatrix
{
    private readonly RoutePlanningOptions _options = options.Value;

    public int TravelMinutes(GeoPoint from, GeoPoint to)
    {
        var km = TravelKilometers(from, to);
        if (km <= 0.01)
            return 1;
        var hours = km / Math.Max(5, _options.AverageSpeedKph);
        return Math.Max(1, (int)Math.Ceiling(hours * 60));
    }

    public double TravelKilometers(GeoPoint from, GeoPoint to) =>
        GeoMath.HaversineKilometers(from, to) * _options.RoadDistanceFactor;
}
