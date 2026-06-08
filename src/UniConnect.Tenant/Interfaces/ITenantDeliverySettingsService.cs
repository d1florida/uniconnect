using UniConnect.Tenant.DTOs;

namespace UniConnect.Tenant.Interfaces;

public interface ITenantDeliverySettingsService
{
    Task<TenantDeliverySettingsDto> GetMySettingsAsync(CancellationToken ct = default);
    Task<TenantDeliverySettingsDto> UpdateMySettingsAsync(UpdateTenantDeliverySettingsRequest request, CancellationToken ct = default);
    Task<TenantDeliverySettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default);
}
