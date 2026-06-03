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
public class TenantsController(
    ITenantService tenantService,
    ITenantApiKeyService apiKeyService,
    ITenantUserService tenantUserService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantDto>>> GetTenants([FromQuery] ProductModule? module, CancellationToken ct) =>
        Ok(await tenantService.GetTenantsAsync(module, ct));

    [HttpGet("dashboard")]
    public async Task<ActionResult<TenantDashboardDto>> GetDashboard(CancellationToken ct) =>
        Ok(await tenantService.GetDashboardAsync(ct));

    [HttpGet("me")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<MyTenantProfileDto>> GetMyTenant(CancellationToken ct) =>
        Ok(await tenantService.GetMyTenantAsync(ct));

    [HttpPatch("me")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantDto>> UpdateMyTenant([FromBody] UpdateMyTenantRequest request, CancellationToken ct) =>
        Ok(await tenantService.UpdateMyTenantAsync(request, ct));

    [HttpPatch("me/admin")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantDto>> UpdateMyTenantAdmin([FromBody] UpdateMyTenantAdminRequest request, CancellationToken ct) =>
        Ok(await tenantService.UpdateMyTenantAdminAsync(request, ct));

    [HttpGet("me/api-keys")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<IReadOnlyList<TenantApiKeyDto>>> GetMyApiKeys(CancellationToken ct) =>
        Ok(await apiKeyService.GetMyKeysAsync(ct));

    [HttpPost("me/api-keys")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<CreateTenantApiKeyResponse>> CreateMyApiKey([FromBody] CreateTenantApiKeyRequest request, CancellationToken ct) =>
        Ok(await apiKeyService.CreateMyKeyAsync(request, ct));

    [HttpDelete("me/api-keys/{keyId:guid}")]
    [Tags("Tenant — Settings")]
    public async Task<IActionResult> RevokeMyApiKey(Guid keyId, CancellationToken ct)
    {
        await apiKeyService.RevokeMyKeyAsync(keyId, ct);
        return NoContent();
    }

    [HttpGet("me/users")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<IReadOnlyList<TenantUserDto>>> GetMyUsers(CancellationToken ct) =>
        Ok(await tenantUserService.GetMyUsersAsync(ct));

    [HttpPost("me/users")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantUserDto>> CreateMyUser([FromBody] CreateTenantUserRequest request, CancellationToken ct) =>
        Ok(await tenantUserService.CreateMyUserAsync(request, ct));

    [HttpPatch("me/users/{userId:guid}")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantUserDto>> UpdateMyUser(Guid userId, [FromBody] UpdateTenantUserRequest request, CancellationToken ct) =>
        Ok(await tenantUserService.UpdateMyUserAsync(userId, request, ct));

    [HttpDelete("me/users/{userId:guid}")]
    [Tags("Tenant — Settings")]
    public async Task<IActionResult> DeleteMyUser(Guid userId, CancellationToken ct)
    {
        await tenantUserService.DeleteMyUserAsync(userId, ct);
        return NoContent();
    }

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
