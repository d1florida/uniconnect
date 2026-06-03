namespace UniConnect.Insights.DTOs;

public record CustomerDto(Guid Id, string Name, string? Phone, string? ExternalRef);

public record DriverDto(Guid Id, string DisplayName, Guid? UserId, string? LinkedUserName, bool IsActive, DateTime CreatedAt);

public record CreateDriverRequest(string DisplayName, Guid? UserId);

public record UpdateDriverRequest(string DisplayName, Guid? UserId, bool IsActive);

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
