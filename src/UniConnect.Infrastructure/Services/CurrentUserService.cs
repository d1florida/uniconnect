using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UniConnect.Application;
using UniConnect.Application.Interfaces;
using UniConnect.Tenant;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(
        User?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User?.FindFirstValue(ClaimTypes.NameIdentifier),
        out var id) ? id : null;

    public Guid? TenantId
    {
        get
        {
            if (Guid.TryParse(User?.FindFirstValue("tenant_id"), out var id))
                return id;
            if (Guid.TryParse(User?.FindFirstValue("fleet_id"), out var legacyId))
                return legacyId;
            return null;
        }
    }

    public ProductModule ProductModules
    {
        get
        {
            var raw = User?.FindFirstValue("product_modules") ?? User?.FindFirstValue("fleet_modules");
            if (int.TryParse(raw, out var flags) && flags != 0)
                return (ProductModule)flags;
            if (Enum.TryParse<ProductModule>(User?.FindFirstValue("fleet_type"), ignoreCase: true, out var single)
                && single != ProductModule.None)
                return single;
            return ProductModule.None;
        }
    }

    public string? Email => User?.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User?.FindFirstValue(ClaimTypes.Email);

    public string? DisplayName => User?.FindFirstValue(JwtRegisteredClaimNames.Name) ?? User?.FindFirstValue(ClaimTypes.Name);

    public bool IsPlatformAdmin =>
        User?.IsInRole("PlatformAdmin") == true || User?.FindFirstValue("role") == "PlatformAdmin";

    public bool IsApiKeyAuth => User?.FindFirstValue("auth_type") == "ApiKey";

    public Guid? ApiKeyId => Guid.TryParse(User?.FindFirstValue("api_key_id"), out var id) ? id : null;

    public TenantRole? TenantRole =>
        Enum.TryParse<TenantRole>(User?.FindFirstValue("tenant_role"), ignoreCase: true, out var role) ? role : null;

    public bool IsTenantAdmin => TenantRole == UniConnect.Tenant.Enums.TenantRole.Admin;

    public bool IsDriver => TenantRole == UniConnect.Tenant.Enums.TenantRole.Driver;

    public Guid? DriverId => Guid.TryParse(User?.FindFirstValue("driver_id"), out var id) ? id : null;

    public void EnsureTenantAdmin()
    {
        if (IsPlatformAdmin) return;
        if (!IsTenantAdmin)
            throw new ForbiddenException("Tenant administrator access is required.");
    }

    public bool HasModule(ProductModule module) => ProductModuleHelper.HasModule(ProductModules, module);

    public void EnsureModule(ProductModule module)
    {
        if (IsPlatformAdmin) return;
        if (!HasModule(module))
            throw new ForbiddenException($"The {module} module is required.");
    }

    public void EnsureModules(params ProductModule[] modules)
    {
        if (IsPlatformAdmin) return;
        foreach (var module in modules)
        {
            if (!HasModule(module))
                throw new ForbiddenException($"The {module} module is required.");
        }
    }

    public void EnsureTenantAccess(Guid tenantId)
    {
        if (IsPlatformAdmin) return;
        if (TenantId != tenantId)
            throw new ForbiddenException("You do not have access to this tenant.");
    }

    public void EnsureVehicleTenantAccess(Guid vehicleTenantId) => EnsureTenantAccess(vehicleTenantId);

    public void EnsureDispatcher()
    {
        if (IsPlatformAdmin) return;
        if (IsDriver)
            throw new ForbiddenException("Driver accounts cannot perform this action.");
    }

    public void EnsureDriverSelf(Guid driverId)
    {
        if (IsPlatformAdmin || !IsDriver) return;
        if (!DriverId.HasValue || DriverId.Value != driverId)
            throw new ForbiddenException("You can only access your own driver record.");
    }
}
