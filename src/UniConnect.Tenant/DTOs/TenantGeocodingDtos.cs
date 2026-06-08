using UniConnect.Tenant.Enums;

namespace UniConnect.Tenant.DTOs;

public record TenantGeocodingSettingsDto(
    GeocodingProvider Provider,
    bool AllowPostalFallback,
    string NominatimUserAgent,
    string? NominatimBaseUrl,
    bool GoogleApiKeyConfigured,
    string? GoogleApiKeyHint,
    bool GoogleApiKeyDecryptFailed,
    bool IsConfigured,
    DateTime? UpdatedAt);

public record UpdateTenantGeocodingSettingsRequest(
    GeocodingProvider Provider,
    bool AllowPostalFallback,
    string? NominatimUserAgent,
    string? NominatimBaseUrl,
    /// <summary>Null = unchanged, empty = clear, non-empty = set new key.</summary>
    string? GoogleApiKey = null);

public record TestTenantGeocodingRequest(string Address);

public record TestTenantGeocodingResultDto(
    string Address,
    bool Success,
    decimal? Latitude,
    decimal? Longitude,
    string? StandardizedAddress,
    string? Source,
    string Message);
