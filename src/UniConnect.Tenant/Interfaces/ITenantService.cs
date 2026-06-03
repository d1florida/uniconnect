using UniConnect.Tenant.DTOs;
using UniConnect.Tenant.Enums;

namespace UniConnect.Tenant.Interfaces;

public interface ITenantService
{
    Task<IReadOnlyList<TenantDto>> GetTenantsAsync(ProductModule? module = null, CancellationToken ct = default);
    Task<TenantDto?> GetTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken ct = default);
    Task DeleteTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantDashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<MyTenantProfileDto> GetMyTenantAsync(CancellationToken ct = default);
    Task<TenantDto> UpdateMyTenantAsync(UpdateMyTenantRequest request, CancellationToken ct = default);
    Task<TenantDto> UpdateMyTenantAdminAsync(UpdateMyTenantAdminRequest request, CancellationToken ct = default);
}
