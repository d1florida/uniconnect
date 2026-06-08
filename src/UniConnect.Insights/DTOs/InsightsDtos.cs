namespace UniConnect.Insights.DTOs;

public record CustomerDto(
    Guid Id,
    string Name,
    string? Phone,
    string? ExternalRef,
    string? DeliveryAddress,
    decimal? DeliveryLatitude,
    decimal? DeliveryLongitude,
    string? DeliveryHours,
    string? DeliveryWindowStart,
    string? DeliveryWindowEnd,
    string? NoDeliveryStart,
    string? NoDeliveryEnd,
    Guid? DeliveryZoneId,
    string? DeliveryZoneName,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt);

public record CreateCustomerRequest(
    string Name,
    string? Phone,
    string? ExternalRef,
    string? DeliveryAddress,
    string? DeliveryHours,
    string? DeliveryWindowStart,
    string? DeliveryWindowEnd,
    string? NoDeliveryStart,
    string? NoDeliveryEnd,
    Guid? DeliveryZoneId = null,
    string? Notes = null);

public record UpdateCustomerRequest(
    string Name,
    string? Phone,
    string? ExternalRef,
    string? DeliveryAddress,
    string? DeliveryHours,
    string? DeliveryWindowStart,
    string? DeliveryWindowEnd,
    string? NoDeliveryStart,
    string? NoDeliveryEnd,
    Guid? DeliveryZoneId = null,
    string? Notes = null,
    bool IsActive = true);

public record DriverDto(
    Guid Id,
    string DisplayName,
    Guid? UserId,
    string? LinkedUserName,
    bool IsActive,
    string ShiftStartTime,
    string ShiftEndTime,
    int LunchMinutes,
    int BreakMinutes,
    int AvailableWorkMinutes,
    int? MaxRouteMinutes,
    string? ReturnByTime,
    DateTime CreatedAt);

public record CreateDriverRequest(
    string DisplayName,
    Guid? UserId,
    string? ShiftStartTime = null,
    string? ShiftEndTime = null,
    int? LunchMinutes = null,
    int? BreakMinutes = null,
    int? MaxRouteMinutes = null,
    string? ReturnByTime = null);

public record UpdateDriverRequest(
    string DisplayName,
    Guid? UserId,
    bool IsActive,
    string? ShiftStartTime = null,
    string? ShiftEndTime = null,
    int? LunchMinutes = null,
    int? BreakMinutes = null,
    int? MaxRouteMinutes = null,
    string? ReturnByTime = null);

public record DriverWorkPatternDayDto(
    int DayOfWeek,
    bool IsWorkingDay,
    string ShiftStartTime,
    string ShiftEndTime,
    int LunchMinutes,
    int BreakMinutes,
    int AvailableWorkMinutes,
    int? MaxRouteMinutes,
    string? ReturnByTime);

public record UpdateDriverWorkPatternDayRequest(
    int DayOfWeek,
    bool IsWorkingDay,
    string? ShiftStartTime = null,
    string? ShiftEndTime = null,
    int? LunchMinutes = null,
    int? BreakMinutes = null,
    int? MaxRouteMinutes = null,
    string? ReturnByTime = null);

public record ResolvedDriverScheduleDto(
    Guid DriverId,
    string DisplayName,
    DateOnly Date,
    bool IsWorking,
    string? ShiftStartTime,
    string? ShiftEndTime,
    int AvailableWorkMinutes,
    int? MaxRouteMinutes,
    string? ReturnByTime,
    string Source = "pattern",
    bool HasException = false,
    string? Note = null);

public record DriverCalendarFixedRouteDto(Guid TemplateId, string Name);

public record DriverCalendarAssignedRouteDto(
    Guid RouteId,
    string Name,
    string Status,
    int StopCount);

public record DriverCalendarDayDto(
    DateOnly Date,
    bool IsWorking,
    string? ShiftStartTime,
    string? ShiftEndTime,
    int AvailableWorkMinutes,
    int? MaxRouteMinutes,
    string? ReturnByTime,
    string Source,
    bool HasException,
    Guid? ScheduleExceptionId,
    string? Note,
    string? OffBlockStartTime,
    string? OffBlockEndTime,
    IReadOnlyList<DriverCalendarFixedRouteDto> FixedRoutes,
    IReadOnlyList<DriverCalendarAssignedRouteDto> AssignedRoutes);

public record DriverCalendarRowDto(
    Guid DriverId,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<DriverCalendarDayDto> Days);

public record DriverCalendarDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<DriverCalendarRowDto> Drivers);

public record UpsertDriverScheduleExceptionRequest(
    DateOnly Date,
    bool IsWorking,
    string? ShiftStartTime = null,
    string? ShiftEndTime = null,
    int? LunchMinutes = null,
    int? BreakMinutes = null,
    int? MaxRouteMinutes = null,
    string? ReturnByTime = null,
    string? OffBlockStartTime = null,
    string? OffBlockEndTime = null,
    string? Note = null);

public record BulkUpsertDriverScheduleExceptionRequest(
    DateOnly From,
    DateOnly To,
    bool IsWorking,
    string? ShiftStartTime = null,
    string? ShiftEndTime = null,
    int? LunchMinutes = null,
    int? BreakMinutes = null,
    int? MaxRouteMinutes = null,
    string? ReturnByTime = null,
    string? Note = null);

public record CreateDriverScheduleRequestRequest(
    Guid DriverId,
    DateOnly FromDate,
    DateOnly ToDate,
    string RequestType,
    string? Note = null);

public record ReviewDriverScheduleRequestRequest(string? ReviewNote = null);

public record DriverScheduleRequestDto(
    Guid Id,
    Guid DriverId,
    string DriverName,
    DateOnly FromDate,
    DateOnly ToDate,
    string RequestType,
    string Status,
    string? Note,
    string RequestedByName,
    DateTime CreatedAt,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewNote);

public record DriverScheduleExceptionDto(
    Guid Id,
    Guid DriverId,
    DateOnly Date,
    bool IsWorking,
    string ShiftStartTime,
    string ShiftEndTime,
    int LunchMinutes,
    int BreakMinutes,
    int AvailableWorkMinutes,
    int? MaxRouteMinutes,
    string? ReturnByTime,
    string? OffBlockStartTime,
    string? OffBlockEndTime,
    string? Note,
    DateTime UpdatedAt);

public record OperationalEventDto(
    Guid Id,
    DateTime OccurredAt,
    string Domain,
    string EventType,
    Guid? OrderId,
    Guid? RouteId,
    Guid? StopId,
    Guid? VehicleId,
    Guid? DriverId,
    Guid? CustomerId,
    Guid? UserId,
    Guid? PlanRunId,
    string? DriverLabel,
    string? VehicleLabel,
    string? CustomerLabel,
    string? PlannerLabel,
    string? Narrative,
    string MetricsJson,
    string ContextJson);

public record InsightsKpiDto(
    int EventCount,
    int OrdersDelivered,
    int OrdersFailed,
    int StopsCompleted,
    int RoutesCompleted,
    double? OnTimeRate,
    double? AvgDelayMinutes);

public record SubjectSummaryDto(
    string SubjectType,
    Guid SubjectId,
    string Label,
    DateOnly From,
    DateOnly To,
    InsightsKpiDto Kpis,
    IReadOnlyList<OperationalEventDto> NotableEvents);

public record PlannerSummaryDto(
    Guid UserId,
    string DisplayName,
    string? Email,
    DateOnly From,
    DateOnly To,
    int PlansRequested,
    int PlansAccepted,
    int PlansDiscarded,
    int OrdersPlanned,
    double AcceptRate,
    IReadOnlyList<OperationalEventDto> NotableEvents);

public record AnalyticsReportBundleDto(
    string SchemaVersion,
    string ReportType,
    Guid TenantId,
    DateOnly From,
    DateOnly To,
    ReportSubjectDto Subject,
    string ExecutiveSummary,
    IReadOnlyDictionary<string, object?> Kpis,
    IReadOnlyList<OperationalEventDto> NotableEvents,
    IReadOnlyDictionary<string, object?>? Comparison);

public record ReportSubjectDto(string Type, Guid Id, string Label, string? Role);

public record TenantDigestDto(
    Guid TenantId,
    DateOnly From,
    DateOnly To,
    InsightsKpiDto Delivery,
    PlannerDigestDto? Planning);

public record PlannerDigestDto(
    int ActivePlanners,
    int PlansRequested,
    int PlansAccepted,
    double AcceptRate);

public record RecordOperationalEventRequest(
    Guid TenantId,
    string Domain,
    string EventType,
    Guid? OrderId = null,
    Guid? RouteId = null,
    Guid? StopId = null,
    Guid? VehicleId = null,
    Guid? DriverId = null,
    Guid? CustomerId = null,
    Guid? UserId = null,
    Guid? PlanRunId = null,
    string? DriverLabel = null,
    string? VehicleLabel = null,
    string? CustomerLabel = null,
    string? PlannerLabel = null,
    string? Narrative = null,
    IReadOnlyDictionary<string, object?>? Metrics = null,
    IReadOnlyDictionary<string, object?>? Context = null);
