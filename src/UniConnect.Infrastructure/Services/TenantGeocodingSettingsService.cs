using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Options;
using UniConnect.Tenant.DTOs;
using UniConnect.Tenant.Entities;
using UniConnect.Tenant.Enums;
using UniConnect.Tenant.Interfaces;

namespace UniConnect.Infrastructure.Services;

public sealed class TenantGeocodingSettingsService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IOptions<RoutePlanningOptions> options,
    ITenantSecretProtector secretProtector,
    TenantGeocodingConfigProvider configProvider,
    IGeocodingService geocoding) : ITenantGeocodingSettingsService
{
    public async Task<TenantGeocodingSettingsDto> GetMySettingsAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantAdminId();
        return await GetSettingsDtoAsync(tenantId, ct);
    }

    public async Task<TenantGeocodingSettingsDto> UpdateMySettingsAsync(
        UpdateTenantGeocodingSettingsRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantAdminId();

        var userAgent = string.IsNullOrWhiteSpace(request.NominatimUserAgent)
            ? options.Value.NominatimUserAgent
            : request.NominatimUserAgent.Trim();

        var baseUrl = string.IsNullOrWhiteSpace(request.NominatimBaseUrl)
            ? null
            : request.NominatimBaseUrl.Trim().TrimEnd('/');

        var entity = await db.TenantGeocodingSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (entity is null)
        {
            entity = new TenantGeocodingSettings { TenantId = tenantId };
            db.TenantGeocodingSettings.Add(entity);
        }

        ApplyGoogleApiKey(entity, request.GoogleApiKey);

        entity.Provider = request.Provider;
        entity.AllowPostalFallback = request.AllowPostalFallback;
        entity.NominatimUserAgent = userAgent;
        entity.NominatimBaseUrl = baseUrl;

        EnsureGoogleProviderHasKey(entity);

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = currentUser.UserId;

        await db.SaveChangesAsync(ct);
        return await GetSettingsDtoAsync(tenantId, ct);
    }

    public async Task<TestTenantGeocodingResultDto> TestMyGeocodingAsync(
        TestTenantGeocodingRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantAdminId();
        var address = request.Address.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            return new TestTenantGeocodingResultDto(
                address, false, null, null, null, null, "Address is required.");
        }

        var result = await geocoding.GeocodeAsync(address, ct, forceRefresh: true, tenantId: tenantId);
        if (!result.HasValue)
        {
            return new TestTenantGeocodingResultDto(
                address,
                false,
                null,
                null,
                null,
                null,
                geocoding.GetLastErrorMessage()
                    ?? "Could not locate this address with the current provider settings.");
        }

        var cacheKey = await configProvider.ResolveCacheKeyAsync(address, tenantId, ct);
        var source = await db.GeocodedAddresses.AsNoTracking()
            .Where(g => g.NormalizedAddress == cacheKey)
            .Select(g => g.Source)
            .FirstOrDefaultAsync(ct);

        var formatted = result.Value.FormattedAddress?.Trim();
        var message = DescribeTestResult(source, formatted, address);

        return new TestTenantGeocodingResultDto(
            address,
            true,
            result.Value.Latitude,
            result.Value.Longitude,
            formatted,
            source,
            message);
    }

    private static string DescribeTestResult(string? source, string? formatted, string address)
    {
        if (formatted is not null && !address.Equals(formatted, StringComparison.OrdinalIgnoreCase))
            return "Match found. Standardized address returned.";
        return source?.ToLowerInvariant() switch
        {
            "google" => "Google Maps street-level match.",
            "census" => "US Census street-level match.",
            "nominatim" => "OpenStreetMap street-level match.",
            "postal" => "Matched to postal code area only.",
            "deterministic" => "Approximate location only.",
            _ => "Match found.",
        };
    }

    private void ApplyGoogleApiKey(TenantGeocodingSettings entity, string? googleApiKey)
    {
        if (googleApiKey is null)
            return;

        if (string.IsNullOrWhiteSpace(googleApiKey))
        {
            entity.GoogleApiKeyProtected = null;
            entity.GoogleApiKeyHint = null;
            return;
        }

        var trimmed = googleApiKey.Trim();
        entity.GoogleApiKeyProtected = secretProtector.Protect(trimmed);
        entity.GoogleApiKeyHint = secretProtector.CreateHint(trimmed);
    }

    private static void EnsureGoogleProviderHasKey(TenantGeocodingSettings entity)
    {
        if (entity.Provider != GeocodingProvider.GoogleMaps)
            return;

        if (string.IsNullOrWhiteSpace(entity.GoogleApiKeyProtected))
            throw new InvalidOperationException("Google Maps requires an API key. Enter your Geocoding API key and save.");
    }

    private async Task<TenantGeocodingSettingsDto> GetSettingsDtoAsync(Guid tenantId, CancellationToken ct)
    {
        var row = await db.TenantGeocodingSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (row is null)
        {
            var defaults = await configProvider.GetConfigAsync(tenantId, ct);
            return new TenantGeocodingSettingsDto(
                defaults.Provider,
                defaults.AllowPostalFallback,
                defaults.NominatimUserAgent,
                defaults.NominatimBaseUrl,
                GoogleApiKeyConfigured: false,
                GoogleApiKeyHint: null,
                GoogleApiKeyDecryptFailed: false,
                IsConfigured: false,
                UpdatedAt: null);
        }

        var (_, decryptFailed) = configProvider.ResolveGoogleApiKey(row);
        return new TenantGeocodingSettingsDto(
            row.Provider,
            row.AllowPostalFallback,
            string.IsNullOrWhiteSpace(row.NominatimUserAgent) ? options.Value.NominatimUserAgent : row.NominatimUserAgent,
            row.NominatimBaseUrl,
            !string.IsNullOrWhiteSpace(row.GoogleApiKeyProtected),
            row.GoogleApiKeyHint,
            decryptFailed,
            IsConfigured: true,
            row.UpdatedAt);
    }

    private Guid RequireTenantAdminId()
    {
        currentUser.EnsureTenantAdmin();
        if (currentUser.IsApiKeyAuth)
            throw new InvalidOperationException("API keys cannot manage geocoding settings.");
        return currentUser.TenantId
            ?? throw new InvalidOperationException("Tenant operator account required.");
    }
}
