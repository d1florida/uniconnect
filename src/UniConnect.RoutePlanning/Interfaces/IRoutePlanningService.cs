using UniConnect.RoutePlanning.DTOs;

namespace UniConnect.RoutePlanning.Interfaces;

public interface IRoutePlanningService
{
    Task<RoutePlanRunDto> PlanRoutesAsync(Guid tenantId, PlanRoutesRequest request, CancellationToken ct = default);
    Task<RoutePlanRunDto?> GetPlanRunAsync(Guid planRunId, CancellationToken ct = default);
    Task<IReadOnlyList<RoutePlanRunDto>> GetPlanRunsAsync(Guid tenantId, CancellationToken ct = default);
    Task<AcceptPlanResultDto> AcceptPlanAsync(Guid planRunId, AcceptPlanRequest request, CancellationToken ct = default);
    Task<RoutePlanRunDto> DiscardPlanAsync(Guid planRunId, DiscardPlanRequest request, CancellationToken ct = default);
    Task<OptimizeSequenceResultDto> OptimizeRouteSequenceAsync(Guid routeId, OptimizeSequenceRequest request, CancellationToken ct = default);
    Task<PlanReadinessDto> GetPlanReadinessAsync(Guid tenantId, Guid? depotId = null, CancellationToken ct = default);
}
