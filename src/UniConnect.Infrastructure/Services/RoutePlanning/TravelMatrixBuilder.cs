using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.RoutePlanning.Options;
using UniConnect.RoutePlanning.Routing;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public sealed class TravelMatrixBuilder(
    IOptions<RoutePlanningOptions> options,
    IHttpClientFactory httpClientFactory,
    HaversineTravelMatrix haversine,
    ILogger<TravelMatrixBuilder> logger) : ITravelMatrixBuilder
{
    private const int OsrmTableCoordinateLimit = 100;

    public async Task<ITravelTimeMatrix> BuildAsync(IReadOnlyList<GeoPoint> points, CancellationToken ct = default)
    {
        if (points.Count == 0)
            return PrecomputedTravelMatrix.Build(points, haversine);

        if (!options.Value.TravelTimeProvider.Equals("Osrm", StringComparison.OrdinalIgnoreCase)
            || points.Count > OsrmTableCoordinateLimit)
        {
            return PrecomputedTravelMatrix.Build(points, haversine);
        }

        var table = await TryBuildOsrmTableAsync(points, ct);
        if (table is null)
            return PrecomputedTravelMatrix.Build(points, haversine);

        return table;
    }

    private async Task<PrecomputedTravelMatrix?> TryBuildOsrmTableAsync(
        IReadOnlyList<GeoPoint> points,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient(OsrmTravelMatrix.HttpClientName);
            var coordinates = string.Join(
                ";",
                points.Select(FormatCoord));

            var path = string.Create(
                CultureInfo.InvariantCulture,
                $"table/v1/driving/{coordinates}?annotations=duration,distance");

            using var response = await client.GetAsync(path, ct);
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                logger.LogWarning("OSRM table unavailable ({StatusCode}); using Haversine matrix.", response.StatusCode);
                return null;
            }

            response.EnsureSuccessStatusCode();
            var payload = await JsonSerializer.DeserializeAsync<OsrmTableResponse>(
                await response.Content.ReadAsStreamAsync(ct),
                cancellationToken: ct);

            if (payload?.Code != "Ok"
                || payload.Durations is null
                || payload.Distances is null
                || payload.Durations.Count != points.Count
                || payload.Distances.Count != points.Count)
            {
                logger.LogWarning(
                    "OSRM table returned {Code} for {Count} points; using Haversine matrix.",
                    payload?.Code ?? "null",
                    points.Count);
                return null;
            }

            var minutes = new int[points.Count, points.Count];
            var kilometers = new double[points.Count, points.Count];

            for (var i = 0; i < points.Count; i++)
            {
                if (payload.Durations[i].Count != points.Count || payload.Distances[i].Count != points.Count)
                    return null;

                for (var j = 0; j < points.Count; j++)
                {
                    var durationSeconds = payload.Durations[i][j];
                    var distanceMeters = payload.Distances[i][j];
                    if (double.IsNaN(durationSeconds) || double.IsNaN(distanceMeters))
                    {
                        minutes[i, j] = haversine.TravelMinutes(points[i], points[j]);
                        kilometers[i, j] = haversine.TravelKilometers(points[i], points[j]);
                        continue;
                    }

                    minutes[i, j] = i == j ? 0 : Math.Max(1, (int)Math.Ceiling(durationSeconds / 60.0));
                    kilometers[i, j] = i == j ? 0 : distanceMeters / 1000.0;
                }
            }

            logger.LogDebug("Built OSRM travel matrix for {Count} planning points.", points.Count);
            return PrecomputedTravelMatrix.FromTable(points, minutes, kilometers, haversine);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "OSRM table request failed; using Haversine matrix.");
            return null;
        }
    }

    private static string FormatCoord(GeoPoint point) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{(double)point.Longitude:F6},{(double)point.Latitude:F6}");

    private sealed class OsrmTableResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; init; }

        [JsonPropertyName("durations")]
        public List<List<double>>? Durations { get; init; }

        [JsonPropertyName("distances")]
        public List<List<double>>? Distances { get; init; }
    }
}
