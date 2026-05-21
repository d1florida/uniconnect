using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UniConnect.Application.Interfaces;
using UniConnect.Domain.Enums;

namespace UniConnect.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(
        User?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User?.FindFirstValue(ClaimTypes.NameIdentifier),
        out var id) ? id : null;

    public Guid? FleetId => Guid.TryParse(User?.FindFirstValue("fleet_id"), out var id) ? id : null;

    public FleetType? FleetType => Enum.TryParse<FleetType>(User?.FindFirstValue("fleet_type"), out var t) ? t : null;

    public string? Email => User?.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User?.FindFirstValue(ClaimTypes.Email);

    public string? DisplayName => User?.FindFirstValue(JwtRegisteredClaimNames.Name) ?? User?.FindFirstValue(ClaimTypes.Name);

    public bool IsPlatformAdmin =>
        User?.IsInRole("PlatformAdmin") == true || User?.FindFirstValue("role") == "PlatformAdmin";

    public void EnsureFleetAccess(Guid fleetId)
    {
        if (IsPlatformAdmin) return;
        if (FleetId != fleetId)
            throw new UnauthorizedAccessException("You do not have access to this fleet.");
    }

    public void EnsureVehicleFleetAccess(Guid vehicleFleetId) => EnsureFleetAccess(vehicleFleetId);
}
