using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Tenant.Entities;

public class TenantPlanningRules
{
    public Guid TenantId { get; set; }
    public string Markdown { get; set; } = string.Empty;
    public string? CompiledPolicyJson { get; set; }
    public DateTime? CompiledAt { get; set; }
    public string? CompileWarningsJson { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public TenantEntity Tenant { get; set; } = null!;
}
