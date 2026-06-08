using UniConnect.GeneralFleet.DTOs;

namespace UniConnect.GeneralFleet.Interfaces;

public interface IGeneralFleetService
{
    Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(Guid tenantId, CancellationToken ct = default);
    Task<VehicleDto?> GetVehicleAsync(Guid vehicleId, CancellationToken ct = default);
    Task<VehicleDto> CreateVehicleAsync(Guid tenantId, CreateVehicleRequest request, CancellationToken ct = default);
    Task<VehicleDto> UpdateVehicleAsync(Guid vehicleId, UpdateVehicleRequest request, CancellationToken ct = default);
    Task DeleteVehicleAsync(Guid vehicleId, CancellationToken ct = default);
    Task<IReadOnlyList<MaintenanceRecordDto>> GetMaintenanceAsync(Guid vehicleId, CancellationToken ct = default);
    Task<MaintenanceRecordDto> CreateMaintenanceAsync(Guid vehicleId, CreateMaintenanceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<FleetVehicleTrackingDto>> GetFleetTrackingAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<VehicleLocationDto>> GetLocationHistoryAsync(Guid vehicleId, CancellationToken ct = default);
    Task<VehicleLocationDto> RecordLocationAsync(Guid vehicleId, RecordLocationRequest request, CancellationToken ct = default);
}
