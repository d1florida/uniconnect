using UniConnect.Domain.Enums;

namespace UniConnect.Domain.Entities;

public class MaintenanceRecord
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public ServiceType ServiceType { get; set; }
    public DateOnly PerformedOn { get; set; }
    public int MileageAtService { get; set; }
    public decimal Cost { get; set; }
    public string Notes { get; set; } = string.Empty;
    public MaintenanceStatus Status { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}
