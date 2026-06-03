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
    string? ParcelDescription = null);

public record PlannedRouteProposalDto(
    Guid? VehicleId,
    string? VehicleLabel,
    int EstimatedMinutes,
    IReadOnlyList<PlannedStopDto> Stops);

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
    int MaxStopsPerRoute = 25,
    string? DepotAddress = null,
    Guid? DepotId = null);

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
    bool CanPlan,
    IReadOnlyList<string> Notes);
