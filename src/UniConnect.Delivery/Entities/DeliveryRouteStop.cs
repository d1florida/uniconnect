using UniConnect.Delivery.Enums;

namespace UniConnect.Delivery.Entities;

public class DeliveryRouteStop
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public int Sequence { get; set; }
    public DeliveryStopType StopType { get; set; }
    public DeliveryStopStatus Status { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string? RecipientPhone { get; set; }
    public string? ParcelDescription { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DeliveryRoute Route { get; set; } = null!;
}
