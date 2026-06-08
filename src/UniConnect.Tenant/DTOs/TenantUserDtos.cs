using UniConnect.Tenant.Enums;

namespace UniConnect.Tenant.DTOs;

public record TenantUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    TenantRole Role,
    IReadOnlyList<ProductModule> ModuleAccess,
    bool IsActive,
    Guid? LinkedDriverId = null,
    string? LinkedDriverName = null);

public record CreateTenantUserRequest(
    string Email,
    string DisplayName,
    TenantRole Role,
    IReadOnlyList<ProductModule> ModuleAccess,
    string Password);

public record UpdateTenantUserRequest(
    string? DisplayName,
    string? Email,
    string? NewPassword,
    TenantRole? Role,
    IReadOnlyList<ProductModule>? ModuleAccess,
    bool? IsActive);
