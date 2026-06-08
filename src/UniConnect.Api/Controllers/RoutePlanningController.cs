using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Interfaces;
using UniConnect.RoutePlanning.DTOs;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.Tenant.Enums;

namespace UniConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/route-planning")]
[Tags("Route planning")]
public class RoutePlanningController(IRoutePlanningService routePlanning, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("tenants/{tenantId:guid}/plan")]
    public async Task<ActionResult<RoutePlanRunDto>> PlanRoutes(
        Guid tenantId,
        [FromBody] PlanRoutesRequest request,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        return Ok(await routePlanning.PlanRoutesAsync(tenantId, request, ct));
    }

    [HttpGet("tenants/{tenantId:guid}/plan-runs")]
    public async Task<ActionResult<IReadOnlyList<RoutePlanRunDto>>> GetPlanRuns(Guid tenantId, CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        return Ok(await routePlanning.GetPlanRunsAsync(tenantId, ct));
    }

    [HttpGet("tenants/{tenantId:guid}/readiness")]
    public async Task<ActionResult<PlanReadinessDto>> GetPlanReadiness(
        Guid tenantId,
        [FromQuery] Guid? depotId,
        [FromQuery] Guid? fixedRouteTemplateId,
        [FromQuery] DateOnly? scheduledDate,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        return Ok(await routePlanning.GetPlanReadinessAsync(tenantId, depotId, fixedRouteTemplateId, scheduledDate, ct));
    }

    [HttpGet("plan-runs/{planRunId:guid}")]
    public async Task<ActionResult<RoutePlanRunDto>> GetPlanRun(Guid planRunId, CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        var run = await routePlanning.GetPlanRunAsync(planRunId, ct);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpPost("plan-runs/{planRunId:guid}/accept")]
    public async Task<ActionResult<AcceptPlanResultDto>> AcceptPlan(
        Guid planRunId,
        [FromBody] AcceptPlanRequest request,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        return Ok(await routePlanning.AcceptPlanAsync(planRunId, request, ct));
    }

    [HttpPost("plan-runs/{planRunId:guid}/discard")]
    public async Task<ActionResult<RoutePlanRunDto>> DiscardPlan(
        Guid planRunId,
        [FromBody] DiscardPlanRequest request,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        return Ok(await routePlanning.DiscardPlanAsync(planRunId, request, ct));
    }

    [HttpPost("routes/{routeId:guid}/optimize-sequence")]
    public async Task<ActionResult<OptimizeSequenceResultDto>> OptimizeSequence(
        Guid routeId,
        [FromBody] OptimizeSequenceRequest request,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        return Ok(await routePlanning.OptimizeRouteSequenceAsync(routeId, request, ct));
    }

    [HttpPost("plan-runs/{planRunId:guid}/explain")]
    public async Task<ActionResult<PlanRunExplanationDto>> ExplainPlanRun(Guid planRunId, CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        return Ok(await routePlanning.ExplainPlanRunAsync(planRunId, ct));
    }
}
