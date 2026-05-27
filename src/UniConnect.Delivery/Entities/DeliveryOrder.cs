using UniConnect.Delivery.Enums;
using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Delivery.Entities;

public class DeliveryOrder
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DeliveryChannel Channel { get; set; }
    public DeliveryOrderStatus Status { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public Guid? BusinessAccountId { get; set; }
    public string ParcelDescription { get; set; } = string.Empty;
    public DateTime? ScheduledWindowStart { get; set; }
    public DateTime? ScheduledWindowEnd { get; set; }
    public DateTime CreatedAt { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
    public BusinessAccount? BusinessAccount { get; set; }
    public DeliveryAssignment? Assignment { get; set; }
}
