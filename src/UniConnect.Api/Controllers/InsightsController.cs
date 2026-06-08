using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Interfaces;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Interfaces;
using UniConnect.Tenant.Enums;

namespace UniConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/insights")]
[Tags("Insights")]
public class InsightsController(IInsightsService insights, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("tenants/{tenantId:guid}/digest")]
    public async Task<ActionResult<TenantDigestDto>> GetDigest(
        Guid tenantId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        return Ok(await insights.GetTenantDigestAsync(tenantId, start, end, ct));
    }

    [HttpGet("tenants/{tenantId:guid}/customers")]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetCustomers(Guid tenantId, CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights);
        return Ok(await insights.GetCustomersAsync(tenantId, ct));
    }

    [HttpGet("tenants/{tenantId:guid}/drivers")]
    public async Task<ActionResult<IReadOnlyList<DriverDto>>> GetDrivers(Guid tenantId, CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights);
        return Ok(await insights.GetDriversAsync(tenantId, ct));
    }

    [HttpGet("tenants/{tenantId:guid}/drivers/{driverId:guid}/summary")]
    public async Task<ActionResult<SubjectSummaryDto>> GetDriverSummary(
        Guid tenantId,
        Guid driverId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        var summary = await insights.GetDriverSummaryAsync(tenantId, driverId, start, end, ct);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("tenants/{tenantId:guid}/vehicles/{vehicleId:guid}/summary")]
    public async Task<ActionResult<SubjectSummaryDto>> GetVehicleSummary(
        Guid tenantId,
        Guid vehicleId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        var summary = await insights.GetVehicleSummaryAsync(tenantId, vehicleId, start, end, ct);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("tenants/{tenantId:guid}/customers/{customerId:guid}/summary")]
    public async Task<ActionResult<SubjectSummaryDto>> GetCustomerSummary(
        Guid tenantId,
        Guid customerId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        var summary = await insights.GetCustomerSummaryAsync(tenantId, customerId, start, end, ct);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("tenants/{tenantId:guid}/planners")]
    public async Task<ActionResult<IReadOnlyList<PlannerSummaryDto>>> GetPlanners(
        Guid tenantId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights, ProductModule.RoutePlanning);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        return Ok(await insights.GetPlannersAsync(tenantId, start, end, ct));
    }

    [HttpGet("tenants/{tenantId:guid}/planners/{userId:guid}/summary")]
    public async Task<ActionResult<PlannerSummaryDto>> GetPlannerSummary(
        Guid tenantId,
        Guid userId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights, ProductModule.RoutePlanning);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        var summary = await insights.GetPlannerSummaryAsync(tenantId, userId, start, end, ct);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpGet("tenants/{tenantId:guid}/reports/{reportType}")]
    public async Task<ActionResult<AnalyticsReportBundleDto>> GetReport(
        Guid tenantId,
        string reportType,
        [FromQuery] Guid subjectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights);
        if (reportType is "planner.activity")
            currentUser.EnsureModule(ProductModule.RoutePlanning);

        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        var id = reportType == "tenant.operations_digest" ? tenantId : subjectId;
        var report = await insights.GetReportAsync(reportType, tenantId, id, start, end, ct);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("tenants/{tenantId:guid}/events")]
    public async Task<ActionResult<IReadOnlyList<OperationalEventDto>>> GetEvents(
        Guid tenantId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? domain,
        [FromQuery] Guid? driverId,
        [FromQuery] Guid? vehicleId,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? userId,
        CancellationToken ct)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-30);
        return Ok(await insights.GetEventsAsync(tenantId, start, end, domain, driverId, vehicleId, customerId, userId, ct));
    }
}
