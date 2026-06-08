using UniConnect.Delivery.Enums;
using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Delivery.Entities;

public class DeliveryZone
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DeliveryZoneMatchType MatchType { get; set; } = DeliveryZoneMatchType.Manual;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
}
