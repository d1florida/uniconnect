using UniConnect.Insights.Enums;

namespace UniConnect.Insights.Entities;

/// <summary>Operator-submitted PTO/time-off request pending admin approval.</summary>
public class DriverScheduleRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public DriverScheduleRequestType RequestType { get; set; }
    public string? Note { get; set; }
    public DriverScheduleRequestStatus Status { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }

    public Driver Driver { get; set; } = null!;
}
