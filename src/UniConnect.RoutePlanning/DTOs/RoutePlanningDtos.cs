using UniConnect.RoutePlanning.Enums;

namespace UniConnect.RoutePlanning.DTOs;

public record PlannedStopDto(
    Guid? OrderId,
    string StopType,
    int Sequence,
    string Address,
    string? RecipientName,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? ParcelDescription = null,
    string? DeliveryOpenStart = null,
    string? DeliveryOpenEnd = null,
    string? NoDeliveryStart = null,
    string? NoDeliveryEnd = null,
    string? EstimatedArrival = null,
    IReadOnlyList<string>? WindowWarnings = null);

public record PlannedRouteProposalDto(
    Guid? VehicleId,
    string? VehicleLabel,
    int EstimatedMinutes,
    IReadOnlyList<PlannedStopDto> Stops,
    Guid? DriverId = null,
    string? DriverLabel = null,
    int? ShiftAvailableMinutes = null,
    string? ShiftWindow = null,
    string? EstimatedRouteStart = null,
    IReadOnlyList<string>? Warnings = null,
    int WindowViolationCount = 0);

public record RoutePlanRunDto(
    Guid Id,
    Guid TenantId,
    Guid RequestedByUserId,
    string RequestedByName,
    RoutePlanRunStatus Status,
    DateOnly ScheduledDate,
    string DepotAddress,
    decimal? DepotLatitude,
    decimal? DepotLongitude,
    int OrdersRequested,
    int OrdersPlanned,
    int OrdersUnassigned,
    int ProposalCount,
    long? ComputeDurationMs,
    IReadOnlyList<PlannedRouteProposalDto> Proposals,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime? ResolvedAt);

public record PlanRoutesRequest(
    DateOnly ScheduledDate,
    IReadOnlyList<Guid>? OrderIds,
    IReadOnlyList<Guid>? VehicleIds,
    IReadOnlyList<Guid>? DriverIds = null,
    int MaxStopsPerRoute = 25,
    string? DepotAddress = null,
    Guid? DepotId = null,
    Guid? FixedRouteTemplateId = null);

public record AcceptPlanRequest(
    IReadOnlyList<Guid>? ProposalVehicleIds);

public record AcceptedRouteSummaryDto(
    Guid Id,
    string Name);

public record AcceptPlanResultDto(
    RoutePlanRunDto PlanRun,
    IReadOnlyList<AcceptedRouteSummaryDto> CreatedRoutes);

public record DiscardPlanRequest(string? Reason);

public record OptimizeSequenceRequest(
    IReadOnlyList<Guid> StopIds);

public record OptimizeSequenceResultDto(
    Guid RouteId,
    IReadOnlyList<Guid> OptimizedStopIds,
    int EstimatedMinutesBefore,
    int EstimatedMinutesAfter);

public record PlanReadinessDto(
    int ReadyOrderCount,
    int PlannableOrderCount,
    int VehicleCount,
    int ActiveVehicleCount,
    int DriverCount,
    int ActiveDriverCount,
    bool CanPlan,
    IReadOnlyList<string> Notes,
    int WindowedOrderCount = 0,
    int HeldOrderCount = 0,
    Guid? FixedRouteTemplateId = null,
    int FixedRouteDueOrderCount = 0,
    int WorkingDriverCount = 0,
    int DriversOffCount = 0,
    DateOnly? ScheduledDate = null);

public record PlanRunExplanationDto(
    Guid PlanRunId,
    string Explanation,
    bool UsedAi);
