using UniConnect.Application.DTOs;
using UniConnect.Domain.Enums;

namespace UniConnect.Application.Interfaces;

public interface IFleetService
{
    Task<IReadOnlyList<FleetDto>> GetFleetsAsync(FleetType? fleetType, CancellationToken ct = default);
    Task<FleetDto> CreateFleetAsync(string name, string slug, FleetType fleetType, CancellationToken ct = default);
    Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(Guid fleetId, CancellationToken ct = default);
    Task<VehicleDto?> GetVehicleAsync(Guid vehicleId, CancellationToken ct = default);
    Task<VehicleDto> CreateVehicleAsync(Guid fleetId, CreateVehicleRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MaintenanceRecordDto>> GetMaintenanceAsync(Guid vehicleId, CancellationToken ct = default);
    Task<MaintenanceRecordDto> CreateMaintenanceAsync(Guid vehicleId, CreateMaintenanceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<FleetVehicleTrackingDto>> GetFleetTrackingAsync(Guid fleetId, CancellationToken ct = default);
    Task<IReadOnlyList<VehicleLocationDto>> GetLocationHistoryAsync(Guid vehicleId, CancellationToken ct = default);
    Task<VehicleLocationDto> RecordLocationAsync(Guid vehicleId, RecordLocationRequest request, CancellationToken ct = default);
}
