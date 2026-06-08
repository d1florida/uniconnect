using UniConnect.Delivery.Enums;
using UniConnect.GeneralFleet.Enums;
using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Delivery.Entities;

public class DeliveryRoute
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DeliveryRouteStatus Status { get; set; }
    public string DepotAddress { get; set; } = string.Empty;
    public Guid? DepotId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public AutomationMode? AutomationMode { get; set; }
    public Guid? RoutePlanRunId { get; set; }
    public Guid? FixedRouteTemplateId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
    public ICollection<DeliveryRouteStop> Stops { get; set; } = [];
}
