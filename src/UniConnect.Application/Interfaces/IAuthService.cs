using UniConnect.Application.DTOs;

namespace UniConnect.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default);
}
