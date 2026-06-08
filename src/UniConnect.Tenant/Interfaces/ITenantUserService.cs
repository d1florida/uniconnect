using UniConnect.Tenant.DTOs;

namespace UniConnect.Tenant.Interfaces;

public interface ITenantUserService
{
    Task<IReadOnlyList<TenantUserDto>> GetMyUsersAsync(CancellationToken ct = default);
    Task<TenantUserDto> CreateMyUserAsync(CreateTenantUserRequest request, CancellationToken ct = default);
    Task<TenantUserDto> UpdateMyUserAsync(Guid userId, UpdateTenantUserRequest request, CancellationToken ct = default);
    Task DeleteMyUserAsync(Guid userId, CancellationToken ct = default);
}
