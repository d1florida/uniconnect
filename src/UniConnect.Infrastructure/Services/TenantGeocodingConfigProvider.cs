using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.RoutePlanning.Options;
using UniConnect.Tenant.Entities;
using UniConnect.Tenant.Enums;
using UniConnect.Tenant.Interfaces;
using UniConnect.Tenant.Models;

namespace UniConnect.Infrastructure.Services;

public sealed class TenantGeocodingConfigProvider(
    AppDbContext db,
    IOptions<RoutePlanningOptions> options,
    ITenantSecretProtector secretProtector,
    ILogger<TenantGeocodingConfigProvider> logger) : ITenantGeocodingConfigProvider
{
    public async Task<TenantGeocodingConfig> GetConfigAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (tenantId is null)
            return PlatformLegacyConfig(opts);

        var row = await db.TenantGeocodingSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, ct);

        if (row is null)
            return PlatformLegacyConfig(opts);

        return MapRow(row, opts);
    }

    public async Task<string> ResolveCacheKeyAsync(string address, Guid? tenantId, CancellationToken ct = default)
    {
        var normalized = address.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            return normalized;

        var config = await GetConfigAsync(tenantId, ct);
        return BuildCacheKey(normalized, tenantId, config);
    }

    internal static string BuildCacheKey(string normalized, Guid? tenantId, TenantGeocodingConfig config)
    {
        if (tenantId is null || config.UseLegacyStack)
            return normalized;

        return $"{tenantId:N}:{(int)config.Provider}:{normalized}";
    }

    private TenantGeocodingConfig MapRow(TenantGeocodingSettings row, RoutePlanningOptions opts)
    {
        var defaultUserAgent = opts.NominatimUserAgent;
        var (googleKey, decryptFailed) = ResolveGoogleApiKey(row);
        if (decryptFailed)
        {
            logger.LogWarning(
                "Could not decrypt the stored Google Maps API key for tenant {TenantId}. " +
                "Re-enter the key under Settings → Geocoding.",
                row.TenantId);
        }

        return new TenantGeocodingConfig(
            row.Provider,
            row.AllowPostalFallback,
            UseLegacyStack: false,
            string.IsNullOrWhiteSpace(row.NominatimUserAgent) ? defaultUserAgent : row.NominatimUserAgent.Trim(),
            string.IsNullOrWhiteSpace(row.NominatimBaseUrl) ? null : row.NominatimBaseUrl.Trim().TrimEnd('/'),
            googleKey,
            decryptFailed);
    }

    internal (string? Key, bool DecryptFailed) ResolveGoogleApiKey(TenantGeocodingSettings row)
    {
        if (row.Provider != GeocodingProvider.GoogleMaps
            || string.IsNullOrWhiteSpace(row.GoogleApiKeyProtected))
            return (null, false);

        if (secretProtector.TryUnprotect(row.GoogleApiKeyProtected, out var googleKey))
            return (googleKey, false);

        return (null, true);
    }

    private static TenantGeocodingConfig PlatformLegacyConfig(RoutePlanningOptions opts)
    {
        var useNominatim = opts.GeocodingProvider.Trim().Equals("Nominatim", StringComparison.OrdinalIgnoreCase);
        if (!useNominatim)
        {
            return new TenantGeocodingConfig(
                GeocodingProvider.OpenStreetMap,
                AllowPostalFallback: false,
                UseLegacyStack: false,
                opts.NominatimUserAgent,
                null);
        }

        return new TenantGeocodingConfig(
            GeocodingProvider.OpenStreetMap,
            AllowPostalFallback: true,
            UseLegacyStack: true,
            opts.NominatimUserAgent,
            null);
    }
}
