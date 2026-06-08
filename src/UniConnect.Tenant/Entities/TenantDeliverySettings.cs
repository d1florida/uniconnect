using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Tenant.Entities;

public class TenantDeliverySettings
{
    public Guid TenantId { get; set; }
    public bool AllowMultipleRoutesPerDriverPerDay { get; set; }
    public string TimeZoneId { get; set; } = "America/New_York";
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
}
