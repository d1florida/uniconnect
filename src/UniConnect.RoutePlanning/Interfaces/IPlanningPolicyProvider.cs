using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Interfaces;

public interface IPlanningPolicyProvider
{
    Task<PlanningPolicy> GetPolicyForTenantAsync(Guid tenantId, CancellationToken ct = default);
}
