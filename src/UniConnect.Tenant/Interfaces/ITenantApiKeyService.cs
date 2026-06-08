using UniConnect.Tenant.DTOs;

namespace UniConnect.Tenant.Interfaces;

public interface ITenantApiKeyService
{
    Task<IReadOnlyList<TenantApiKeyDto>> GetMyKeysAsync(CancellationToken ct = default);
    Task<CreateTenantApiKeyResponse> CreateMyKeyAsync(CreateTenantApiKeyRequest request, CancellationToken ct = default);
    Task RevokeMyKeyAsync(Guid keyId, CancellationToken ct = default);
}
