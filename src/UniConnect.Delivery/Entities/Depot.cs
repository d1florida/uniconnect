using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Delivery.Entities;

public class Depot
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsDefault { get; set; }
    public string? Hours { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
}
