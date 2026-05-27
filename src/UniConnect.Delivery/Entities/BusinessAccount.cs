using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Delivery.Entities;

public class BusinessAccount
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;

    public TenantEntity Tenant { get; set; } = null!;
}
