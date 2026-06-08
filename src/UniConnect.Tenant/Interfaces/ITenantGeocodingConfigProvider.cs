using UniConnect.Tenant.Models;

namespace UniConnect.Tenant.Interfaces;

public interface ITenantGeocodingConfigProvider
{
    Task<TenantGeocodingConfig> GetConfigAsync(Guid? tenantId, CancellationToken ct = default);
    Task<string> ResolveCacheKeyAsync(string address, Guid? tenantId, CancellationToken ct = default);
}
