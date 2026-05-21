using UniConnect.Application.DTOs;
using UniConnect.RoboTaxi.Enums;

namespace UniConnect.RoboTaxi.DTOs;

public record RoboTaxiProfileDto(
    Guid VehicleId,
    AutonomyLevel AutonomyLevel,
    OperationalState OperationalState,
    string SoftwareVersion,
    int? BatteryPercent,
    int PassengerCapacity,
    DateTime? LastDisengagementAt,
    GroundedReason GroundedReason);

public record RoboTaxiVehicleDto(
    Guid Id,
    Guid FleetId,
    string Vin,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    int CurrentMileage,
    RoboTaxiProfileDto Profile,
    LocationDto? LatestLocation);

public record CreateRoboTaxiVehicleRequest(
    string Vin,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    int CurrentMileage,
    AutonomyLevel AutonomyLevel,
    string SoftwareVersion,
    int PassengerCapacity);

public record UpdateRoboTaxiStateRequest(
    OperationalState OperationalState,
    GroundedReason? GroundedReason,
    int? BatteryPercent);

public record RoboTaxiTrackingDto(
    Guid VehicleId,
    string LicensePlate,
    OperationalState OperationalState,
    LocationDto? LatestLocation);

public record RoboTaxiDashboardDto(
    int TotalVehicles,
    int Idle,
    int OnTrip,
    int Charging,
    int Maintenance,
    int Grounded,
    int StaleLocation);
