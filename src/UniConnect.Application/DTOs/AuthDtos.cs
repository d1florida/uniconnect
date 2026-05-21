using UniConnect.Domain.Enums;

namespace UniConnect.Application.DTOs;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, DateTime ExpiresAt, UserProfileDto User);

public record UserProfileDto(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid? FleetId,
    string? FleetName,
    FleetType? FleetType,
    bool IsPlatformAdmin);
