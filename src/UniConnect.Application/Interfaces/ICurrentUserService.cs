using UniConnect.Tenant.Enums;

namespace UniConnect.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    ProductModule ProductModules { get; }
    string? Email { get; }
    string? DisplayName { get; }
    bool IsPlatformAdmin { get; }
    bool IsApiKeyAuth { get; }
    Guid? ApiKeyId { get; }
    TenantRole? TenantRole { get; }
    bool IsTenantAdmin { get; }
    bool HasModule(ProductModule module);
    void EnsureModule(ProductModule module);
    void EnsureModules(params ProductModule[] modules);
    void EnsureTenantAccess(Guid tenantId);
    void EnsureVehicleTenantAccess(Guid vehicleTenantId);
    void EnsureTenantAdmin();
}
