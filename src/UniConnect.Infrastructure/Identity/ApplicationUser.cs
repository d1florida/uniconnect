using Microsoft.AspNetCore.Identity;

using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public TenantRole TenantRole { get; set; } = TenantRole.Operator;
    public ProductModule ModuleAccess { get; set; } = ProductModule.None;
    public bool IsActive { get; set; } = true;
}
