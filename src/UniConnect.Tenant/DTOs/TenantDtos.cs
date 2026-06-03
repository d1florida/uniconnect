using UniConnect.Tenant.Enums;

namespace UniConnect.Tenant.DTOs;

public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    IReadOnlyList<ProductModule> Modules,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    string? AdminEmail,
    DateTime CreatedAt);

public record CreateTenantRequest(
    string Name,
    string Slug,
    IReadOnlyList<ProductModule> Modules,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string AdminEmail = "",
    string AdminPassword = "");

public record UpdateTenantRequest(
    string? Name,
    string? Slug,
    IReadOnlyList<ProductModule>? Modules,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? AdminEmail = null,
    string? AdminPassword = null);

public record TenantDashboardDto(
    int TotalTenants,
    int GeneralTenants,
    int RoboTaxiTenants,
    int DeliveryTenants);

public record PasswordPolicyDto(
    int MinLength,
    bool RequireDigit,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireNonAlphanumeric);

public record MyTenantProfileDto(
    TenantDto Tenant,
    PasswordPolicyDto PasswordPolicy,
    TenantRole TenantRole,
    bool IsTenantAdmin,
    IReadOnlyList<ProductModule> ModuleAccess);

public record UpdateMyTenantRequest(
    string Name,
    string Slug,
    string ContactName,
    string ContactEmail,
    string ContactPhone);

public record UpdateMyTenantAdminRequest(
    string? Email,
    string? NewPassword);
