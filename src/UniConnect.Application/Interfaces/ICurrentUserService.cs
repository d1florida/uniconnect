using UniConnect.Domain.Enums;

namespace UniConnect.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? FleetId { get; }
    FleetType? FleetType { get; }
    string? Email { get; }
    string? DisplayName { get; }
    bool IsPlatformAdmin { get; }
    void EnsureFleetAccess(Guid fleetId);
    void EnsureVehicleFleetAccess(Guid vehicleFleetId);
}
