using UniConnect.GeneralFleet.Enums;

namespace UniConnect.GeneralFleet.DTOs;

public record VehicleDto(
    Guid Id,
    Guid TenantId,
    string Vin,
    string Make,
    string Model,
    int Year,
    AssetCategory Category,
    string VehicleNumber,
    string LicensePlate,
    int CurrentMileage,
    VehicleStatus Status,
    LocationDto? LatestLocation,
    Guid? HomeDepotId = null,
    string? HomeDepotName = null);

public record CreateVehicleRequest(
    string Vin,
    string Make,
    string Model,
    int Year,
    AssetCategory Category,
    string VehicleNumber,
    string LicensePlate,
    int CurrentMileage);

public record UpdateVehicleRequest(
    string Vin,
    string Make,
    string Model,
    int Year,
    AssetCategory Category,
    string VehicleNumber,
    string LicensePlate,
    int CurrentMileage,
    VehicleStatus Status);

public record MaintenanceRecordDto(
    Guid Id,
    Guid VehicleId,
    ServiceType ServiceType,
    DateOnly PerformedOn,
    int MileageAtService,
    decimal Cost,
    string Notes,
    MaintenanceStatus Status);

public record CreateMaintenanceRequest(
    ServiceType ServiceType,
    DateOnly PerformedOn,
    int MileageAtService,
    decimal Cost,
    string Notes,
    MaintenanceStatus Status);

public record FleetVehicleTrackingDto(
    Guid VehicleId,
    AssetCategory Category,
    string VehicleNumber,
    string LicensePlate,
    VehicleStatus Status,
    LocationDto? LatestLocation);

public record RecordLocationRequest(
    decimal Latitude,
    decimal Longitude,
    decimal? SpeedKph);

public record VehicleLocationDto(
    Guid Id,
    decimal Latitude,
    decimal Longitude,
    DateTime RecordedAt,
    decimal? SpeedKph,
    LocationSource Source);
