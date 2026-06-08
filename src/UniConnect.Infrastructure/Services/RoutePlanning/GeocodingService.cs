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
using UniConnect.Tenant.Enums;
using UniConnect.Tenant.Interfaces;
using UniConnect.Tenant.Models;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public sealed class GeocodingService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IOptions<RoutePlanningOptions> options,
    ITenantGeocodingConfigProvider geocodingConfig,
    ILogger<GeocodingService> logger) : IGeocodingService
{
    public const string CensusClientName = "CensusGeocoder";
    public const string GoogleClientName = "GoogleGeocoder";
    private static readonly SemaphoreSlim NominatimGate = new(1, 1);
    private static DateTime _lastNominatimRequestUtc = DateTime.MinValue;
    private string? _lastErrorMessage;

    public string? GetLastErrorMessage() => _lastErrorMessage;

    public string NormalizeAddress(string address) =>
        address.Trim().ToLowerInvariant();

    public async Task<GeocodeResult?> GeocodeAsync(
        string address,
        CancellationToken ct = default,
        bool forceRefresh = false,
        AddressGeocodeHints hints = default,
        Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        _lastErrorMessage = null;
        var config = await geocodingConfig.GetConfigAsync(tenantId, ct);
        var normalized = NormalizeAddress(address);
        var cacheKey = TenantGeocodingConfigProvider.BuildCacheKey(normalized, tenantId, config);
        var mergedHints = AddressGeocodeHints.Merge(hints, AddressGeocodeHints.Parse(address));
        GeocodedAddress? cached = null;
        if (!forceRefresh)
        {
            cached = await db.GeocodedAddresses
                .FirstOrDefaultAsync(g => g.NormalizedAddress == cacheKey, ct);

            if (cached is not null && !await ShouldUpgradeCachedSourceAsync(cached.Source, address, tenantId, ct))
                return ToResult(cached);
        }
        else
        {
            cached = await db.GeocodedAddresses
                .FirstOrDefaultAsync(g => g.NormalizedAddress == cacheKey, ct);
        }

        var resolved = await ResolveAsync(address, normalized, mergedHints, config, ct);
        if (resolved is null)
            return cached is not null ? ToResult(cached) : null;

        if (cached is null)
        {
            db.GeocodedAddresses.Add(new GeocodedAddress
            {
                NormalizedAddress = cacheKey,
                Latitude = resolved.Value.Latitude,
                Longitude = resolved.Value.Longitude,
                Source = resolved.Value.Source,
                FormattedAddress = resolved.Value.FormattedAddress,
                GeocodedAt = DateTime.UtcNow,
            });
        }
        else
        {
            cached.Latitude = resolved.Value.Latitude;
            cached.Longitude = resolved.Value.Longitude;
            cached.Source = resolved.Value.Source;
            cached.FormattedAddress = resolved.Value.FormattedAddress;
            cached.GeocodedAt = DateTime.UtcNow;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsGeocodeCacheDuplicate(ex))
        {
            DetachTrackedGeocodes();
            var existing = await db.GeocodedAddresses.AsNoTracking()
                .FirstOrDefaultAsync(g => g.NormalizedAddress == cacheKey, ct);
            if (existing is not null)
                return ToResult(existing);

            throw;
        }

        return new GeocodeResult(resolved.Value.Latitude, resolved.Value.Longitude, resolved.Value.FormattedAddress);
    }

    public async Task<IReadOnlyDictionary<string, GeocodeResult>> GeocodeManyAsync(
        IEnumerable<string> addresses,
        CancellationToken ct = default,
        Guid? tenantId = null)
    {
        var unique = addresses
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new Dictionary<string, GeocodeResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var address in unique)
        {
            var geocoded = await GeocodeAsync(address, ct, tenantId: tenantId);
            if (geocoded.HasValue)
                result[address] = geocoded.Value;
        }

        return result;
    }

    private static GeocodeResult ToResult(GeocodedAddress cached) =>
        new(cached.Latitude, cached.Longitude, cached.FormattedAddress);

    private void DetachTrackedGeocodes()
    {
        foreach (var entry in db.ChangeTracker.Entries<GeocodedAddress>().ToList())
            entry.State = EntityState.Detached;
    }

    private static bool IsGeocodeCacheDuplicate(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("PK_GeocodedAddresses", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                && message.Contains("GeocodedAddresses", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> ShouldUpgradeCachedSourceAsync(
        string source,
        string address,
        Guid? tenantId = null,
        CancellationToken ct = default)
    {
        var config = await geocodingConfig.GetConfigAsync(tenantId, ct);
        if (config.UseLegacyStack)
            return ShouldUpgradeLegacy(source, address, options.Value);

        if (source.Equals("deterministic", StringComparison.OrdinalIgnoreCase))
            return true;

        return config.Provider switch
        {
            GeocodingProvider.UsCensus when AddressGeocodeHints.Parse(address).HasUsContext() =>
                source.Equals("nominatim", StringComparison.OrdinalIgnoreCase)
                || (source.Equals("postal", StringComparison.OrdinalIgnoreCase)
                    && !AddressGeocodeHints.IsIncomplete(address)),
            GeocodingProvider.OpenStreetMap =>
                AddressGeocodeHints.IsIncomplete(address)
                && source.Equals("nominatim", StringComparison.OrdinalIgnoreCase),
            GeocodingProvider.GoogleMaps =>
                !source.Equals("google", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool ShouldUpgradeLegacy(string source, string address, RoutePlanningOptions opts)
    {
        if (source.Equals("deterministic", StringComparison.OrdinalIgnoreCase))
            return opts.GeocodingProvider.Trim().Equals("Nominatim", StringComparison.OrdinalIgnoreCase);

        if (opts.EnableUsCensusGeocoder
            && opts.GeocodingProvider.Trim().Equals("Nominatim", StringComparison.OrdinalIgnoreCase)
            && AddressGeocodeHints.Parse(address).HasUsContext())
        {
            if (source.Equals("nominatim", StringComparison.OrdinalIgnoreCase))
                return true;

            if (source.Equals("postal", StringComparison.OrdinalIgnoreCase)
                && !AddressGeocodeHints.IsIncomplete(address))
                return true;
        }

        if (!opts.GeocodingProvider.Trim().Equals("Nominatim", StringComparison.OrdinalIgnoreCase))
            return false;

        return AddressGeocodeHints.IsIncomplete(address)
            && source.Equals("nominatim", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> ResolveAsync(
        string address,
        string normalized,
        AddressGeocodeHints hints,
        TenantGeocodingConfig config,
        CancellationToken ct)
    {
        if (config.UseLegacyStack)
            return await ResolveLegacyAsync(address, normalized, hints, ct);

        return config.Provider switch
        {
            GeocodingProvider.UsCensus => await ResolveCensusOnlyAsync(address, hints, config, ct),
            GeocodingProvider.OpenStreetMap => await ResolveOpenStreetMapAsync(address, hints, config, ct),
            GeocodingProvider.GoogleMaps => await ResolveGoogleOnlyAsync(address, hints, config, ct),
            _ => GeoMath.DeterministicGeocode(normalized, hints) is var d
                ? (d.Latitude, d.Longitude, "deterministic", null)
                : null,
        };
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> ResolveLegacyAsync(
        string address,
        string normalized,
        AddressGeocodeHints hints,
        CancellationToken ct)
    {
        var opts = options.Value;
        if (!opts.GeocodingProvider.Trim().Equals("Nominatim", StringComparison.OrdinalIgnoreCase))
        {
            var deterministic = GeoMath.DeterministicGeocode(normalized, hints);
            return (deterministic.Latitude, deterministic.Longitude, "deterministic", null);
        }

        if (opts.EnableUsCensusGeocoder && hints.HasUsContext())
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
            var nominatim = await TryNominatimAsync(query, hints, TenantGeocodingConfigExtensions.Legacy(opts), ct);
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
            var postal = await TryNominatimPostalAsync(hints, TenantGeocodingConfigExtensions.Legacy(opts), ct);
            if (postal.HasValue)
                return postal;

            var localPostal = TryLocalPostalCentroid(hints);
            if (localPostal.HasValue)
                return localPostal;
        }

        logger.LogWarning("Geocoding returned no result for {Address}; using deterministic fallback.", address);
        var fallback = GeoMath.DeterministicGeocode(normalized, hints);
        return (fallback.Latitude, fallback.Longitude, "deterministic", null);
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> ResolveCensusOnlyAsync(
        string address,
        AddressGeocodeHints hints,
        TenantGeocodingConfig config,
        CancellationToken ct)
    {
        if (hints.HasUsContext())
        {
            foreach (var query in BuildSearchQueries(address, hints))
            {
                var census = await TryCensusAsync(query, ct);
                if (census.HasValue)
                    return census;
            }
        }

        if (config.AllowPostalFallback && hints.PostalCode is not null)
        {
            var postal = await TryNominatimPostalAsync(hints, config, ct);
            if (postal.HasValue)
                return postal;

            var localPostal = TryLocalPostalCentroid(hints);
            if (localPostal.HasValue)
                return localPostal;
        }

        logger.LogWarning("Census geocoding returned no result for {Address}.", address);
        return null;
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> ResolveOpenStreetMapAsync(
        string address,
        AddressGeocodeHints hints,
        TenantGeocodingConfig config,
        CancellationToken ct)
    {
        foreach (var query in BuildSearchQueries(address, hints))
        {
            var nominatim = await TryNominatimAsync(query, hints, config, ct);
            if (!nominatim.HasValue)
                continue;

            var enriched = hints.Enrich(address.Trim());
            if (AddressGeocodeHints.IsIncomplete(address)
                && query.Equals(address.Trim(), StringComparison.OrdinalIgnoreCase)
                && !query.Equals(enriched, StringComparison.OrdinalIgnoreCase))
                continue;

            return nominatim;
        }

        if (config.AllowPostalFallback && hints.PostalCode is not null)
        {
            var postal = await TryNominatimPostalAsync(hints, config, ct);
            if (postal.HasValue)
                return postal;

            var localPostal = TryLocalPostalCentroid(hints);
            if (localPostal.HasValue)
                return localPostal;
        }

        logger.LogWarning("OpenStreetMap geocoding returned no result for {Address}.", address);
        return null;
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> ResolveGoogleOnlyAsync(
        string address,
        AddressGeocodeHints hints,
        TenantGeocodingConfig config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.GoogleApiKey))
        {
            _lastErrorMessage = config.GoogleApiKeyDecryptFailed
                ? "Stored Google Maps API key could not be read (often after an API restart). Re-enter your key under Settings → Geocoding and save."
                : "Google Maps API key is not configured. Add your key under Settings → Geocoding and save.";
            logger.LogWarning(
                "Google Maps geocoding requested but no usable API key is available for the tenant (decryptFailed={DecryptFailed}).",
                config.GoogleApiKeyDecryptFailed);
            return null;
        }

        foreach (var query in BuildSearchQueries(address, hints))
        {
            var google = await TryGoogleAsync(query, config.GoogleApiKey, ct);
            if (!google.HasValue)
                continue;

            var enriched = hints.Enrich(address.Trim());
            if (AddressGeocodeHints.IsIncomplete(address)
                && query.Equals(address.Trim(), StringComparison.OrdinalIgnoreCase)
                && !query.Equals(enriched, StringComparison.OrdinalIgnoreCase))
                continue;

            return google;
        }

        if (config.AllowPostalFallback && hints.PostalCode is not null)
        {
            var postal = await TryNominatimPostalAsync(hints, config, ct);
            if (postal.HasValue)
                return postal;

            var localPostal = TryLocalPostalCentroid(hints);
            if (localPostal.HasValue)
                return localPostal;
        }

        _lastErrorMessage ??= "Google Maps could not find this address. Check the street, city, and ZIP.";
        logger.LogWarning("Google Maps geocoding returned no result for {Address}.", address);
        return null;
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

    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> TryNominatimPostalAsync(
        AddressGeocodeHints hints,
        TenantGeocodingConfig config,
        CancellationToken ct)
    {
        if (hints.PostalCode is null)
            return null;

        var state = hints.State ?? AddressGeocodeHints.InferStateFromPostalCode(hints.PostalCode);
        var query = hints.City is not null
            ? $"{hints.City}, {state ?? "US"} {hints.PostalCode}"
            : hints.PostalCode;

        var result = await TryNominatimAsync(query, hints, config, ct);
        if (!result.HasValue)
            return null;

        return (result.Value.Latitude, result.Value.Longitude, "postal", result.Value.FormattedAddress);
    }

    private static (decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)? TryLocalPostalCentroid(
        AddressGeocodeHints hints)
    {
        if (hints.PostalCode is null)
            return null;

        var point = GeoMath.TryPostalCentroid(hints.PostalCode);
        if (!point.HasValue)
            return null;

        return (point.Value.Latitude, point.Value.Longitude, "postal", null);
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> TryNominatimAsync(
        string address,
        AddressGeocodeHints hints,
        TenantGeocodingConfig config,
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

                var countryFilter = hints.HasUsContext() ? "&countrycodes=us" : string.Empty;
                var path = $"search?format=json&limit=1{countryFilter}&q={Uri.EscapeDataString(address)}";
                using var request = BuildNominatimRequest(path, config);
                var client = httpClientFactory.CreateClient(nameof(GeocodingService));
                using var response = await client.SendAsync(request, ct);
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

                var formatted = string.IsNullOrWhiteSpace(hit.DisplayName) ? null : hit.DisplayName.Trim();
                return ((decimal)lat, (decimal)lng, "nominatim", formatted);
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

    private static HttpRequestMessage BuildNominatimRequest(string path, TenantGeocodingConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.NominatimBaseUrl))
        {
            var baseUri = config.NominatimBaseUrl.TrimEnd('/') + "/";
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUri), path));
            request.Headers.TryAddWithoutValidation("User-Agent", config.NominatimUserAgent);
            return request;
        }

        var relative = new HttpRequestMessage(HttpMethod.Get, path);
        relative.Headers.TryAddWithoutValidation("User-Agent", config.NominatimUserAgent);
        return relative;
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> TryGoogleAsync(
        string address,
        string apiKey,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient(GoogleClientName);
            var url =
                $"maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(apiKey)}";
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _lastErrorMessage = $"Google Geocoding returned HTTP {(int)response.StatusCode}.";
                logger.LogWarning("Google Geocoding HTTP {StatusCode} for {Address}", (int)response.StatusCode, address);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GoogleGeocodeResponse>(cancellationToken: ct);
            if (payload?.Status == "ZERO_RESULTS")
            {
                _lastErrorMessage = "Google Maps has no match for this address.";
                return null;
            }

            if (payload?.Status != "OK")
            {
                _lastErrorMessage = FormatGoogleError(payload?.Status, payload?.ErrorMessage);
                logger.LogWarning(
                    "Google Geocoding status {Status} for {Address}: {Error}",
                    payload?.Status,
                    address,
                    payload?.ErrorMessage);
                return null;
            }

            var hit = payload.Results?.FirstOrDefault();
            if (hit?.Geometry?.Location is null)
            {
                _lastErrorMessage = "Google Maps returned no coordinates for this address.";
                return null;
            }

            var formatted = string.IsNullOrWhiteSpace(hit.FormattedAddress) ? null : hit.FormattedAddress.Trim();
            return ((decimal)hit.Geometry.Location.Lat, (decimal)hit.Geometry.Location.Lng, "google", formatted);
        }
        catch (Exception ex)
        {
            _lastErrorMessage = "Google Geocoding request failed. Check your API key and network.";
            logger.LogWarning(ex, "Google geocoding failed for {Address}", address);
            return null;
        }
    }

    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> TryCensusAsync(
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

            var formatted = string.IsNullOrWhiteSpace(match.MatchedAddress) ? null : match.MatchedAddress.Trim();
            return ((decimal)match.Coordinates.Y, (decimal)match.Coordinates.X, "census", formatted);
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

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
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
        [JsonPropertyName("matchedAddress")]
        public string? MatchedAddress { get; set; }

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

    private sealed class GoogleGeocodeResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("results")]
        public List<GoogleGeocodeResult>? Results { get; set; }
    }

    private sealed class GoogleGeocodeResult
    {
        [JsonPropertyName("formatted_address")]
        public string? FormattedAddress { get; set; }

        [JsonPropertyName("geometry")]
        public GoogleGeometry? Geometry { get; set; }
    }

    private sealed class GoogleGeometry
    {
        [JsonPropertyName("location")]
        public GoogleLocation? Location { get; set; }
    }

    private sealed class GoogleLocation
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }
    }

    private static string FormatGoogleError(string? status, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
            return errorMessage.Trim();

        return status switch
        {
            "REQUEST_DENIED" =>
                "Google denied the request. Enable the Geocoding API for your key in Google Cloud Console and ensure billing is active.",
            "INVALID_REQUEST" => "Google rejected the address format.",
            "OVER_QUERY_LIMIT" => "Google Maps quota exceeded. Try again later or check billing.",
            _ => $"Google Geocoding failed ({status ?? "unknown"}).",
        };
    }
}

internal static class TenantGeocodingConfigExtensions
{
    public static TenantGeocodingConfig Legacy(RoutePlanningOptions opts) =>
        new(
            GeocodingProvider.OpenStreetMap,
            AllowPostalFallback: true,
            UseLegacyStack: true,
            opts.NominatimUserAgent,
            null,
            null);
}
