namespace UniConnect.Tenant.DTOs;

public record TenantApiKeyDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string KeyPrefix,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt,
    bool IsActive);

public record CreateTenantApiKeyRequest(string Name);

public record CreateTenantApiKeyResponse(TenantApiKeyDto Key, string Secret);
