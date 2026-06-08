using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Routing;

/// <summary>
/// O(1) travel lookups for a fixed set of coordinates. Falls back to a source matrix for unknown points.
/// </summary>
public sealed class PrecomputedTravelMatrix : ITravelTimeMatrix
{
    private readonly IReadOnlyList<GeoPoint> _points;
    private readonly int[,] _minutes;
    private readonly double[,] _kilometers;
    private readonly Dictionary<(long Longitude, long Latitude), int> _indexByCoord;
    private readonly ITravelTimeMatrix? _fallback;

    private PrecomputedTravelMatrix(
        IReadOnlyList<GeoPoint> points,
        int[,] minutes,
        double[,] kilometers,
        ITravelTimeMatrix? fallback)
    {
        _points = points;
        _minutes = minutes;
        _kilometers = kilometers;
        _fallback = fallback;
        _indexByCoord = new Dictionary<(long, long), int>(points.Count);
        for (var i = 0; i < points.Count; i++)
            _indexByCoord[CoordKey(points[i])] = i;
    }

    public static PrecomputedTravelMatrix Build(
        IReadOnlyList<GeoPoint> points,
        ITravelTimeMatrix source)
    {
        if (points.Count == 0)
            return new PrecomputedTravelMatrix(points, new int[0, 0], new double[0, 0], source);

        var minutes = new int[points.Count, points.Count];
        var kilometers = new double[points.Count, points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            minutes[i, i] = 0;
            kilometers[i, i] = 0;
            for (var j = i + 1; j < points.Count; j++)
            {
                var m = source.TravelMinutes(points[i], points[j]);
                var km = source.TravelKilometers(points[i], points[j]);
                minutes[i, j] = m;
                minutes[j, i] = m;
                kilometers[i, j] = km;
                kilometers[j, i] = km;
            }
        }

        return new PrecomputedTravelMatrix(points, minutes, kilometers, source);
    }

    public static PrecomputedTravelMatrix FromTable(
        IReadOnlyList<GeoPoint> points,
        int[,] minutes,
        double[,] kilometers,
        ITravelTimeMatrix? fallback = null) =>
        new(points, minutes, kilometers, fallback);

    public IReadOnlyList<GeoPoint> Points => _points;

    public int TravelMinutes(GeoPoint from, GeoPoint to)
    {
        if (TryGetIndices(from, to, out var i, out var j))
            return _minutes[i, j];
        return _fallback?.TravelMinutes(from, to)
            ?? Math.Max(1, (int)Math.Ceiling(GeoMath.HaversineKilometers(from, to)));
    }

    public double TravelKilometers(GeoPoint from, GeoPoint to)
    {
        if (TryGetIndices(from, to, out var i, out var j))
            return _kilometers[i, j];
        return _fallback?.TravelKilometers(from, to) ?? GeoMath.HaversineKilometers(from, to);
    }

    private bool TryGetIndices(GeoPoint from, GeoPoint to, out int fromIndex, out int toIndex)
    {
        fromIndex = -1;
        toIndex = -1;
        if (!_indexByCoord.TryGetValue(CoordKey(from), out fromIndex))
            return false;
        if (!_indexByCoord.TryGetValue(CoordKey(to), out toIndex))
            return false;
        return true;
    }

    private static (long Longitude, long Latitude) CoordKey(GeoPoint point) =>
        (RoundCoord(point.Longitude), RoundCoord(point.Latitude));

    private static long RoundCoord(decimal value) =>
        (long)Math.Round((double)value * 100_000, MidpointRounding.AwayFromZero);
}
