using UniConnect.Tenant.Enums;

namespace UniConnect.Tenant.Entities;

/// <summary>
/// Platform tenant account. Product-specific data lives in product modules.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ProductModule Modules { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
