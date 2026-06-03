using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.RoutePlanning.Routing;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public sealed class OsrmTravelMatrix(
    IHttpClientFactory httpClientFactory,
    HaversineTravelMatrix haversineFallback,
    ILogger<OsrmTravelMatrix> logger) : ITravelTimeMatrix
{
    public const string HttpClientName = "OsrmRouting";

    private readonly Dictionary<(long, long, long, long), (int Minutes, double Kilometers)> _cache = new();

    public int TravelMinutes(GeoPoint from, GeoPoint to)
    {
        var route = GetOrFetchRoute(from, to);
        return route.Minutes;
    }

    public double TravelKilometers(GeoPoint from, GeoPoint to)
    {
        var route = GetOrFetchRoute(from, to);
        return route.Kilometers;
    }

    private (int Minutes, double Kilometers) GetOrFetchRoute(GeoPoint from, GeoPoint to)
    {
        if (GeoMath.HaversineKilometers(from, to) < 0.01)
            return (1, 0);

        var key = CacheKey(from, to);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var result = FetchFromOsrm(from, to) ?? Fallback(from, to);
        _cache[key] = result;
        return result;
    }

    private (int Minutes, double Kilometers)? FetchFromOsrm(GeoPoint from, GeoPoint to)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var path = string.Create(
                CultureInfo.InvariantCulture,
                $"route/v1/driving/{FormatCoord(from)};{FormatCoord(to)}?overview=false");
            using var response = client.GetAsync(path).GetAwaiter().GetResult();
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                logger.LogWarning("OSRM unavailable ({StatusCode}); using Haversine fallback.", response.StatusCode);
                return null;
            }

            response.EnsureSuccessStatusCode();
            var payload = JsonSerializer.Deserialize<OsrmRouteResponse>(
                response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

            if (payload?.Code != "Ok" || payload.Routes is not { Count: > 0 })
            {
                logger.LogWarning(
                    "OSRM returned {Code} for {From} -> {To}; using Haversine fallback.",
                    payload?.Code ?? "null",
                    from,
                    to);
                return null;
            }

            var route = payload.Routes[0];
            var minutes = Math.Max(1, (int)Math.Ceiling(route.DurationSeconds / 60.0));
            var kilometers = route.DistanceMeters / 1000.0;
            return (minutes, kilometers);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "OSRM request failed for {From} -> {To}; using Haversine fallback.", from, to);
            return null;
        }
    }

    private (int Minutes, double Kilometers) Fallback(GeoPoint from, GeoPoint to) =>
        (haversineFallback.TravelMinutes(from, to), haversineFallback.TravelKilometers(from, to));

    private static string FormatCoord(GeoPoint point) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{(double)point.Longitude:F6},{(double)point.Latitude:F6}");

    private static (long, long, long, long) CacheKey(GeoPoint from, GeoPoint to) =>
        (RoundCoord(from.Latitude), RoundCoord(from.Longitude), RoundCoord(to.Latitude), RoundCoord(to.Longitude));

    private static long RoundCoord(decimal value) =>
        (long)Math.Round((double)value * 100_000, MidpointRounding.AwayFromZero);

    private sealed class OsrmRouteResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("routes")]
        public List<OsrmRoute>? Routes { get; init; }
    }

    private sealed class OsrmRoute
    {
        [JsonPropertyName("duration")]
        public double DurationSeconds { get; init; }

        [JsonPropertyName("distance")]
        public double DistanceMeters { get; init; }
    }
}
