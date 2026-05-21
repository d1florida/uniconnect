using UniConnect.RoboTaxi.Enums;

namespace UniConnect.RoboTaxi.Entities;

public class RoboTaxiProfile
{
    public Guid VehicleId { get; set; }
    public AutonomyLevel AutonomyLevel { get; set; }
    public OperationalState OperationalState { get; set; }
    public string SoftwareVersion { get; set; } = string.Empty;
    public int? BatteryPercent { get; set; }
    public int PassengerCapacity { get; set; }
    public DateTime? LastDisengagementAt { get; set; }
    public GroundedReason GroundedReason { get; set; }
}
