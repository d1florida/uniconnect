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
    /// <summary>High-level equipment classification (tractor, trailer, etc.).</summary>
    public AssetCategory Category { get; set; } = AssetCategory.LightVehicle;
    /// <summary>Short fleet identifier for drivers and dispatch (e.g. "101", "VAN-3").</summary>
    public string VehicleNumber { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public int CurrentMileage { get; set; }
    public VehicleStatus Status { get; set; }
    /// <summary>Home delivery depot when the tenant uses the Delivery module.</summary>
    public Guid? HomeDepotId { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = [];
    public ICollection<VehicleLocation> Locations { get; set; } = [];
}
