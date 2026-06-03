using UniConnect.RoutePlanning.Enums;

namespace UniConnect.RoutePlanning.Entities;

public class RoutePlanRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public RoutePlanRunStatus Status { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public string DepotAddress { get; set; } = string.Empty;
    public Guid? DepotId { get; set; }
    public int OrdersRequested { get; set; }
    public int OrdersPlanned { get; set; }
    public int OrdersUnassigned { get; set; }
    public int ProposalCount { get; set; }
    public long? ComputeDurationMs { get; set; }
    public string? ProposalJson { get; set; }
    public string? DiscardReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
