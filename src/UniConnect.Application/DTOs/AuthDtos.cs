using UniConnect.Tenant.Enums;

namespace UniConnect.Application.DTOs;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, DateTime ExpiresAt, UserProfileDto User);

public record UserProfileDto(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid? TenantId,
    string? TenantName,
    IReadOnlyList<ProductModule> Modules,
    bool IsPlatformAdmin,
    TenantRole? TenantRole,
    bool IsTenantAdmin);
