using UniConnect.Tenant.Enums;
using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Tenant.Entities;

public class TenantGeocodingSettings
{
    public Guid TenantId { get; set; }
    public GeocodingProvider Provider { get; set; } = GeocodingProvider.OpenStreetMap;
    public bool AllowPostalFallback { get; set; } = true;
    public string? NominatimUserAgent { get; set; }
    public string? NominatimBaseUrl { get; set; }
    public string? GoogleApiKeyProtected { get; set; }
    public string? GoogleApiKeyHint { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
}
