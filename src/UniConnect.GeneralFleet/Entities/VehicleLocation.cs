using UniConnect.GeneralFleet.Enums;

namespace UniConnect.GeneralFleet.Entities;

public class VehicleLocation
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime RecordedAt { get; set; }
    public decimal? SpeedKph { get; set; }
    public decimal? Heading { get; set; }
    public LocationSource Source { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}
