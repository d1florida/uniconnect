using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Tenant.DTOs;
using UniConnect.Tenant.Enums;
using UniConnect.Tenant.Interfaces;

namespace UniConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/tenants")]
[Tags("Platform — Tenants")]
public class TenantsController(ITenantService tenantService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantDto>>> GetTenants([FromQuery] ProductModule? module, CancellationToken ct) =>
        Ok(await tenantService.GetTenantsAsync(module, ct));

    [HttpGet("dashboard")]
    public async Task<ActionResult<TenantDashboardDto>> GetDashboard(CancellationToken ct) =>
        Ok(await tenantService.GetDashboardAsync(ct));

    [HttpGet("{tenantId:guid}")]
    public async Task<ActionResult<TenantDto>> GetTenant(Guid tenantId, CancellationToken ct)
    {
        var tenant = await tenantService.GetTenantAsync(tenantId, ct);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    public async Task<ActionResult<TenantDto>> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct) =>
        Ok(await tenantService.CreateTenantAsync(request, ct));

    [HttpPatch("{tenantId:guid}")]
    public async Task<ActionResult<TenantDto>> UpdateTenant(Guid tenantId, [FromBody] UpdateTenantRequest request, CancellationToken ct) =>
        Ok(await tenantService.UpdateTenantAsync(tenantId, request, ct));

    [HttpDelete("{tenantId:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid tenantId, CancellationToken ct)
    {
        await tenantService.DeleteTenantAsync(tenantId, ct);
        return NoContent();
    }
}
