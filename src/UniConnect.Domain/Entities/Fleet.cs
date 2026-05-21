using UniConnect.Domain.Enums;

namespace UniConnect.Domain.Entities;

public class Fleet
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public FleetType FleetType { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = [];
}
