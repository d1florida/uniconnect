using UniConnect.Application.DTOs;
using UniConnect.Delivery.Enums;
using UniConnect.RoboTaxi.Enums;

namespace UniConnect.Delivery.DTOs;

public record BusinessAccountDto(
    Guid Id,
    Guid FleetId,
    string CompanyName,
    string AccountCode,
    string ContactEmail);

public record CreateBusinessAccountRequest(
    string CompanyName,
    string AccountCode,
    string ContactEmail);

public record DeliveryAssignmentDto(
    Guid Id,
    Guid VehicleId,
    string LicensePlate,
    AutomationMode AutomationMode,
    DateTime AssignedAt);

public record DeliveryOrderDto(
    Guid Id,
    Guid FleetId,
    DeliveryChannel Channel,
    DeliveryOrderStatus Status,
    string PickupAddress,
    string DeliveryAddress,
    string RecipientName,
    string RecipientPhone,
    Guid? BusinessAccountId,
    string? BusinessAccountName,
    string ParcelDescription,
    DeliveryAssignmentDto? Assignment,
    DateTime CreatedAt);

public record CreateDeliveryOrderRequest(
    DeliveryChannel Channel,
    string PickupAddress,
    string DeliveryAddress,
    string RecipientName,
    string RecipientPhone,
    Guid? BusinessAccountId,
    string ParcelDescription);

public record AssignDeliveryRequest(Guid VehicleId, AutomationMode AutomationMode);

public record UpdateDeliveryStatusRequest(DeliveryOrderStatus Status);

public record DeliveryVehicleDto(
    Guid Id,
    string LicensePlate,
    string Make,
    string Model,
    bool IsAutonomous,
    OperationalState? OperationalState,
    LocationDto? LatestLocation);

public record DeliveryTrackingDto(
    Guid VehicleId,
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
    int B2BOrders,
    int B2COrders,
    int AutonomousActive,
    int ConventionalActive,
    int ActiveRoutes,
    int PlannedRoutes);

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
    DateTime? CompletedAt);

public record DeliveryRouteDto(
    Guid Id,
    Guid FleetId,
    string Name,
    DeliveryRouteStatus Status,
    string DepotAddress,
    DateOnly ScheduledDate,
    Guid? VehicleId,
    string? LicensePlate,
    AutomationMode? AutomationMode,
    int StopCount,
    int CompletedStops,
    int PendingStops,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public record DeliveryRouteDetailDto(
    Guid Id,
    Guid FleetId,
    string Name,
    DeliveryRouteStatus Status,
    string DepotAddress,
    DateOnly ScheduledDate,
    Guid? VehicleId,
    string? LicensePlate,
    AutomationMode? AutomationMode,
    IReadOnlyList<DeliveryRouteStopDto> Stops,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public record CreateRouteStopRequest(
    DeliveryStopType StopType,
    string Address,
    string? RecipientName,
    string? RecipientPhone,
    string? ParcelDescription,
    string? Notes);

public record CreateDeliveryRouteRequest(
    string Name,
    string DepotAddress,
    DateOnly ScheduledDate,
    IReadOnlyList<CreateRouteStopRequest> Stops);

public record UpdateDeliveryRouteRequest(
    string Name,
    string DepotAddress,
    DateOnly ScheduledDate);

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

public record AssignRouteRequest(Guid VehicleId, AutomationMode AutomationMode);

public record UpdateRouteStatusRequest(DeliveryRouteStatus Status);

public record UpdateStopStatusRequest(DeliveryStopStatus Status);
