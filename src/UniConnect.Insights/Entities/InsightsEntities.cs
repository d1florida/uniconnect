namespace UniConnect.Insights.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? ExternalRef { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Driver
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class OperationalEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string SchemaVersion { get; set; } = "1.0";
    public string Domain { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;

    public Guid? OrderId { get; set; }
    public Guid? RouteId { get; set; }
    public Guid? StopId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? PlanRunId { get; set; }

    public string? DriverLabel { get; set; }
    public string? VehicleLabel { get; set; }
    public string? CustomerLabel { get; set; }
    public string? PlannerLabel { get; set; }

    public string? Narrative { get; set; }
    public string MetricsJson { get; set; } = "{}";
    public string ContextJson { get; set; } = "{}";
}
