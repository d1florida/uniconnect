using UniConnect.Delivery.Enums;
using UniConnect.GeneralFleet.DTOs;
using UniConnect.GeneralFleet.Enums;
using UniConnect.RoboTaxi.Enums;

namespace UniConnect.Delivery.DTOs;

public record DeliveryAssignmentDto(
    Guid Id,
    Guid VehicleId,
    string VehicleNumber,
    string LicensePlate,
    AutomationMode AutomationMode,
    Guid? DriverId,
    string? DriverName,
    DateTime AssignedAt);

public record DeliveryOrderDto(
    Guid Id,
    Guid TenantId,
    DeliveryOrderStatus Status,
    string PickupAddress,
    string DeliveryAddress,
    string RecipientName,
    string RecipientPhone,
    string ParcelDescription,
    decimal? PickupLatitude,
    decimal? PickupLongitude,
    decimal? DeliveryLatitude,
    decimal? DeliveryLongitude,
    string? PickupGeocodeSource,
    string? DeliveryGeocodeSource,
    DeliveryAssignmentDto? Assignment,
    DateTime CreatedAt);

public record CreateDeliveryOrderRequest(
    string PickupAddress,
    string DeliveryAddress,
    string RecipientName,
    string RecipientPhone,
    string ParcelDescription);

public record UpdateDeliveryOrderRequest(
    string PickupAddress,
    string DeliveryAddress,
    string RecipientName,
    string RecipientPhone,
    string ParcelDescription);

public record AssignDeliveryRequest(Guid VehicleId, AutomationMode AutomationMode, Guid? DriverId = null);

public record UpdateDeliveryStatusRequest(DeliveryOrderStatus Status);

public record DeliveryVehicleDto(
    Guid Id,
    AssetCategory Category,
    string VehicleNumber,
    string LicensePlate,
    string Make,
    string Model,
    bool IsAutonomous,
    VehicleStatus Status,
    OperationalState? OperationalState,
    LocationDto? LatestLocation,
    Guid? HomeDepotId = null,
    string? HomeDepotName = null);

public record AssignVehicleHomeDepotRequest(Guid? HomeDepotId);

public record DeliveryTrackingDto(
    Guid VehicleId,
    AssetCategory Category,
    string VehicleNumber,
    string LicensePlate,
    bool IsAutonomous,
    OperationalState? OperationalState,
    LocationDto? LatestLocation,
    Guid? ActiveOrderId,
    DeliveryOrderStatus? ActiveOrderStatus,
    string? DeliveryAddress);

public record DeliveryDashboardDto(
    int OpenOrders,
    int InTransit,
    int TotalOrders,
    int AutonomousActive,
    int ConventionalActive,
    int ActiveRoutes,
    int PlannedRoutes,
    int OrdersReadyToPlan,
    int DraftRoutes,
    int RoutesNeedingDrivers);

public record DepotDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    bool IsDefault,
    string? Hours,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt);

public record CreateDepotRequest(
    string Name,
    string Address,
    bool IsDefault = false,
    string? Hours = null,
    string? Notes = null);

public record UpdateDepotRequest(
    string Name,
    string Address,
    bool IsDefault,
    string? Hours,
    string? Notes,
    bool IsActive);

public record DeliveryRouteStopDto(
    Guid Id,
    int Sequence,
    DeliveryStopType StopType,
    DeliveryStopStatus Status,
    string Address,
    string? RecipientName,
    string? RecipientPhone,
    string? ParcelDescription,
    string? Notes,
    Guid? DeliveryOrderId,
    DateTime? CompletedAt,
    decimal? Latitude = null,
    decimal? Longitude = null);

public record DeliveryRouteDto(
    Guid Id,
    Guid TenantId,
    string Name,
    DeliveryRouteStatus Status,
    string DepotAddress,
    DateOnly ScheduledDate,
    Guid? VehicleId,
    string? VehicleNumber,
    string? LicensePlate,
    Guid? DriverId,
    string? DriverName,
    AutomationMode? AutomationMode,
    int StopCount,
    int CompletedStops,
    int PendingStops,
    Guid? RoutePlanRunId,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public record DeliveryRouteDetailDto(
    Guid Id,
    Guid TenantId,
    string Name,
    DeliveryRouteStatus Status,
    string DepotAddress,
    DateOnly ScheduledDate,
    Guid? VehicleId,
    string? VehicleNumber,
    string? LicensePlate,
    Guid? DriverId,
    string? DriverName,
    AutomationMode? AutomationMode,
    IReadOnlyList<DeliveryRouteStopDto> Stops,
    int? EstimatedDriveMinutes,
    int? EstimatedMinutesToNextStop,
    DateTime? EstimatedNextStopArrivalAt,
    Guid? RoutePlanRunId,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public record CreateRouteStopRequest(
    DeliveryStopType StopType,
    string Address,
    string? RecipientName,
    string? RecipientPhone,
    string? ParcelDescription,
    string? Notes,
    Guid? DeliveryOrderId = null);

public record CreateDeliveryRouteRequest(
    string Name,
    DateOnly ScheduledDate,
    IReadOnlyList<CreateRouteStopRequest> Stops,
    string? DepotAddress = null,
    Guid? DepotId = null);

public record UpdateDeliveryRouteRequest(
    string Name,
    DateOnly ScheduledDate,
    string? DepotAddress = null,
    Guid? DepotId = null);

public record AddRouteStopRequest(
    DeliveryStopType StopType,
    string Address,
    string? RecipientName,
    string? RecipientPhone,
    string? ParcelDescription,
    string? Notes);

public record UpdateRouteStopRequest(
    string Address,
    string? RecipientName,
    string? RecipientPhone,
    string? ParcelDescription,
    string? Notes);

public record ReorderRouteStopsRequest(IReadOnlyList<Guid> StopIds);

public record AssignRouteRequest(Guid VehicleId, AutomationMode AutomationMode, Guid? DriverId = null);

public record UpdateRouteStatusRequest(DeliveryRouteStatus Status);

public record UpdateStopStatusRequest(DeliveryStopStatus Status);
