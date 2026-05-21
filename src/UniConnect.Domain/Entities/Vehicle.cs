using UniConnect.Domain.Enums;

namespace UniConnect.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public Guid FleetId { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public int CurrentMileage { get; set; }
    public VehicleStatus Status { get; set; }

    public Fleet Fleet { get; set; } = null!;
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = [];
    public ICollection<VehicleLocation> Locations { get; set; } = [];
}
