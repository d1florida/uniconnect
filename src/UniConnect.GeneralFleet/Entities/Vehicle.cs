using UniConnect.GeneralFleet.Enums;
using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.GeneralFleet.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public int CurrentMileage { get; set; }
    public VehicleStatus Status { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = [];
    public ICollection<VehicleLocation> Locations { get; set; } = [];
}
