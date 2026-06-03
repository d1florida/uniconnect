using UniConnect.GeneralFleet.Enums;

namespace UniConnect.Delivery.Entities;

public class DeliveryAssignment
{
    public Guid Id { get; set; }
    public Guid DeliveryOrderId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public AutomationMode AutomationMode { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DeliveryOrder DeliveryOrder { get; set; } = null!;
}
