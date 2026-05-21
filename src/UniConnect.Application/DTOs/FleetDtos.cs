using UniConnect.Domain.Enums;

namespace UniConnect.Application.DTOs;

public record FleetDto(Guid Id, string Name, string Slug, FleetType FleetType, DateTime CreatedAt);

public record CreateFleetRequest(string Name, string Slug);

public record VehicleDto(
    Guid Id,
    Guid FleetId,
    string Vin,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    int CurrentMileage,
    VehicleStatus Status,
    LocationDto? LatestLocation);

public record CreateVehicleRequest(
    string Vin,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    int CurrentMileage);

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
