using UniConnect.RoboTaxi.DTOs;

namespace UniConnect.RoboTaxi.Interfaces;

public interface IRoboTaxiService
{
    Task<IReadOnlyList<RoboTaxiVehicleDto>> GetVehiclesAsync(Guid tenantId, CancellationToken ct = default);
    Task<RoboTaxiVehicleDto?> GetVehicleAsync(Guid vehicleId, CancellationToken ct = default);
    Task<RoboTaxiVehicleDto> CreateVehicleAsync(Guid tenantId, CreateRoboTaxiVehicleRequest request, CancellationToken ct = default);
    Task<RoboTaxiProfileDto> UpdateStateAsync(Guid vehicleId, UpdateRoboTaxiStateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<RoboTaxiTrackingDto>> GetTrackingAsync(Guid tenantId, CancellationToken ct = default);
    Task<RoboTaxiDashboardDto> GetDashboardAsync(CancellationToken ct = default);
}
