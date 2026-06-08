using UniConnect.Tenant.DTOs;

namespace UniConnect.Tenant.Interfaces;

public interface ITenantPlanningRulesService
{
    Task<TenantPlanningRulesDto> GetMyRulesAsync(CancellationToken ct = default);
    Task<TenantPlanningRulesDto> UpdateMyRulesAsync(UpdateTenantPlanningRulesRequest request, CancellationToken ct = default);
    Task<CompileTenantPlanningRulesResultDto> CompileMyRulesAsync(CancellationToken ct = default);
}
