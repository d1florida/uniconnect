using UniConnect.Application.DTOs;
using UniConnect.RoboTaxi.DTOs;

namespace UniConnect.RoboTaxi.Interfaces;

public interface IRoboTaxiService
{
    Task<IReadOnlyList<FleetDto>> GetFleetsAsync(CancellationToken ct = default);
    Task<FleetDto> CreateFleetAsync(string name, string slug, CancellationToken ct = default);
    Task<IReadOnlyList<RoboTaxiVehicleDto>> GetVehiclesAsync(Guid fleetId, CancellationToken ct = default);
    Task<RoboTaxiVehicleDto?> GetVehicleAsync(Guid vehicleId, CancellationToken ct = default);
    Task<RoboTaxiVehicleDto> CreateVehicleAsync(Guid fleetId, CreateRoboTaxiVehicleRequest request, CancellationToken ct = default);
    Task<RoboTaxiProfileDto> UpdateStateAsync(Guid vehicleId, UpdateRoboTaxiStateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<RoboTaxiTrackingDto>> GetTrackingAsync(Guid fleetId, CancellationToken ct = default);
    Task<RoboTaxiDashboardDto> GetDashboardAsync(CancellationToken ct = default);
}
