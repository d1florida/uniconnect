using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniConnect.Infrastructure.Data;
using UniConnect.RoutePlanning.Entities;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.RoutePlanning.Options;
using UniConnect.RoutePlanning.Routing;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public sealed class GeocodingService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IOptions<RoutePlanningOptions> options,
    ILogger<GeocodingService> logger) : IGeocodingService
{
    public const string CensusClientName = "CensusGeocoder";
    private static readonly SemaphoreSlim NominatimGate = new(1, 1);
    private static DateTime _lastNominatimRequestUtc = DateTime.MinValue;

    public string NormalizeAddress(string address) =>
        address.Trim().ToLowerInvariant();

    public async Task<GeoPoint?> GeocodeAsync(
        string address,
        CancellationToken ct = default,
        bool forceRefresh = false,
        AddressGeocodeHints hints = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        var normalized = NormalizeAddress(address);
        var mergedHints = AddressGeocodeHints.Merge(hints, AddressGeocodeHints.Parse(address));
        GeocodedAddress? cached = null;
        if (!forceRefresh)
        {
            cached = await db.GeocodedAddresses
                .FirstOrDefaultAsync(g => g.NormalizedAddress == normalized, ct);

            if (cached is not null && !ShouldUpgradeCachedSource(cached.Source, address))
                return new GeoPoint(cached.Latitude, cached.Longitude);
        }
        else
        {
            cached = await db.GeocodedAddresses
                .FirstOrDefaultAsync(g => g.NormalizedAddress == normalized, ct);
        }

        var resolved = await ResolveAsync(address, normalized, mergedHints, ct);
        if (resolved is null)
            return cached is not null ? new GeoPoint(cached.Latitude, cached.Longitude) : null;

        if (cached is null)
        {
            db.GeocodedAddresses.Add(new GeocodedAddress
            {
                NormalizedAddress = normalized,
                Latitude = resolved.Value.Latitude,
                Longitude = resolved.Value.Longitude,
                Source = resolved.Value.Source,
                GeocodedAt = DateTime.UtcNow,
            });
        }
        else
        {
            cached.Latitude = resolved.Value.Latitude;
            cached.Longitude = resolved.Value.Longitude;
            cached.Source = resolved.Value.Source;
            cached.GeocodedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return new GeoPoint(resolved.Value.Latitude, resolved.Value.Longitude);
    }

    public async Task<IReadOnlyDictionary<string, GeoPoint>> GeocodeManyAsync(IEnumerable<string> addresses, CancellationToken ct = default)
    {
        var unique = addresses
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new Dictionary<string, GeoPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var address in unique)
        {
            var point = await GeocodeAsync(address, ct);
            if (point.HasValue)
                result[address] = point.Value;
        }

        return result;
    }

    public bool ShouldUpgradeCachedSource(string source, string address)
    {
        if (source.Equals("deterministic", StringComparison.OrdinalIgnoreCase))
            return UseNominatim();

        if (UseUsCensus() && AddressGeocodeHints.Parse(address).HasUsContext())
        {
            if (source.Equals("nominatim", StringComparison.OrdinalIgnoreCase))
                return true;

            if (source.Equals("postal", StringComparison.OrdinalIgnoreCase)
                && !AddressGeocodeHints.IsIncomplete(address))
                return true;
        }

        if (!UseNominatim())
            return false;

        return AddressGeocodeHints.IsIncomplete(address)
            && source.Equals("nominatim", StringComparison.OrdinalIgnoreCase);
    }

    private bool UseNominatim() =>
        options.Value.GeocodingProvider.Trim().Equals("Nominatim", StringComparison.OrdinalIgnoreCase);

    private bool UseUsCensus() =>
        options.Value.EnableUsCensusGeocoder && UseNominatim();

    private async Task<(decimal Latitude, decimal Longitude, string Source)?> ResolveAsync(
        string address,
        string normalized,
        AddressGeocodeHints hints,
        CancellationToken ct)
    {
        if (UseNominatim())
        {
            if (UseUsCensus() && hints.HasUsContext())
            {
                foreach (var query in BuildSearchQueries(address, hints))
                {
                    var census = await TryCensusAsync(query, ct);
                    if (census.HasValue)
                        return census;
                }
            }

            foreach (var query in BuildSearchQueries(address, hints))
            {
                var nominatim = await TryNominatimAsync(query, hints, ct);
                if (!nominatim.HasValue)
                    continue;

                var enriched = hints.Enrich(address.Trim());
                if (AddressGeocodeHints.IsIncomplete(address)
                    && query.Equals(address.Trim(), StringComparison.OrdinalIgnoreCase)
                    && !query.Equals(enriched, StringComparison.OrdinalIgnoreCase))
                    continue;

                return nominatim;
            }

            if (hints.PostalCode is not null)
            {
                var postal = await TryNominatimPostalAsync(hints, ct);
                if (postal.HasValue)
                    return postal;

                var localPostal = TryLocalPostalCentroid(hints);
                if (localPostal.HasValue)
                    return localPostal;
            }

            logger.LogWarning("Geocoding returned no result for {Address}; using deterministic fallback.", address);
        }

        var deterministic = GeoMath.DeterministicGeocode(normalized, hints);
        return (deterministic.Latitude, deterministic.Longitude, "deterministic");
    }

    private static IEnumerable<string> BuildSearchQueries(string address, AddressGeocodeHints hints)
    {
        var trimmed = address.Trim();
        var enriched = hints.Enrich(trimmed);
        if (AddressGeocodeHints.IsIncomplete(trimmed))
        {
            if (!trimmed.Equals(enriched, StringComparison.OrdinalIgnoreCase))
                yield return enriched;
            yield return trimmed;
            yield break;
        }

        yield return trimmed;
        if (!trimmed.Equals(enriched, StringComparison.OrdinalIgnoreCase))
            yield return enriched;
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source)?> TryNominatimPostalAsync(
        AddressGeocodeHints hints,
        CancellationToken ct)
    {
        if (hints.PostalCode is null)
            return null;

        var state = hints.State ?? AddressGeocodeHints.InferStateFromPostalCode(hints.PostalCode);
        var query = hints.City is not null
            ? $"{hints.City}, {state ?? "US"} {hints.PostalCode}"
            : hints.PostalCode;

        var result = await TryNominatimAsync(query, hints, ct);
        if (!result.HasValue)
            return null;

        return (result.Value.Latitude, result.Value.Longitude, "postal");
    }

    private static (decimal Latitude, decimal Longitude, string Source)? TryLocalPostalCentroid(
        AddressGeocodeHints hints)
    {
        if (hints.PostalCode is null)
            return null;

        var point = GeoMath.TryPostalCentroid(hints.PostalCode);
        if (!point.HasValue)
            return null;

        return (point.Value.Latitude, point.Value.Longitude, "postal");
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source)?> TryNominatimAsync(
        string address,
        AddressGeocodeHints hints,
        CancellationToken ct)
    {
        try
        {
            await NominatimGate.WaitAsync(ct);
            try
            {
                var elapsed = DateTime.UtcNow - _lastNominatimRequestUtc;
                if (elapsed < TimeSpan.FromSeconds(1.1))
                    await Task.Delay(TimeSpan.FromSeconds(1.1) - elapsed, ct);

                var client = httpClientFactory.CreateClient(nameof(GeocodingService));
                var countryFilter = hints.HasUsContext() ? "&countrycodes=us" : string.Empty;
                var url = $"search?format=json&limit=1{countryFilter}&q={Uri.EscapeDataString(address)}";
                using var response = await client.GetAsync(url, ct);
                _lastNominatimRequestUtc = DateTime.UtcNow;

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Nominatim HTTP {StatusCode} for {Address}", (int)response.StatusCode, address);
                    return null;
                }

                var results = await response.Content.ReadFromJsonAsync<List<NominatimResult>>(cancellationToken: ct);
                var hit = results?.FirstOrDefault();
                if (hit is null
                    || !double.TryParse(hit.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                    || !double.TryParse(hit.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
                    return null;

                return ((decimal)lat, (decimal)lng, "nominatim");
            }
            finally
            {
                NominatimGate.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nominatim geocoding failed for {Address}", address);
            return null;
        }
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source)?> TryCensusAsync(
        string address,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient(CensusClientName);
            var url =
                $"locations/onelineaddress?benchmark=Public_AR_Current&format=json&address={Uri.EscapeDataString(address)}";
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Census geocoder HTTP {StatusCode} for {Address}", (int)response.StatusCode, address);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<CensusGeocodeResponse>(cancellationToken: ct);
            var match = payload?.Result?.AddressMatches?.FirstOrDefault();
            if (match?.Coordinates is null)
                return null;

            return ((decimal)match.Coordinates.Y, (decimal)match.Coordinates.X, "census");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Census geocoding failed for {Address}", address);
            return null;
        }
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string Lat { get; set; } = string.Empty;

        [JsonPropertyName("lon")]
        public string Lon { get; set; } = string.Empty;
    }

    private sealed class CensusGeocodeResponse
    {
        [JsonPropertyName("result")]
        public CensusGeocodeResult? Result { get; set; }
    }

    private sealed class CensusGeocodeResult
    {
        [JsonPropertyName("addressMatches")]
        public List<CensusAddressMatch>? AddressMatches { get; set; }
    }

    private sealed class CensusAddressMatch
    {
        [JsonPropertyName("coordinates")]
        public CensusCoordinates? Coordinates { get; set; }
    }

    private sealed class CensusCoordinates
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }
}
