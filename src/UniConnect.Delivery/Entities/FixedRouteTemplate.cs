using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Delivery.Entities;

public class FixedRouteTemplate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid DeliveryZoneId { get; set; }
    public List<DayOfWeek> RouteDays { get; set; } = [];
    public Guid DepotId { get; set; }
    public Guid? DefaultVehicleId { get; set; }
    public Guid? DefaultDriverId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
    public DeliveryZone DeliveryZone { get; set; } = null!;
}
