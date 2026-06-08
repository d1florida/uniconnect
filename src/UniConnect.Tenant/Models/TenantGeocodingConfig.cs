using UniConnect.Tenant.Enums;

namespace UniConnect.Tenant.Models;

public sealed record TenantGeocodingConfig(
    GeocodingProvider Provider,
    bool AllowPostalFallback,
    bool UseLegacyStack,
    string NominatimUserAgent,
    string? NominatimBaseUrl,
    string? GoogleApiKey = null,
    bool GoogleApiKeyDecryptFailed = false);
