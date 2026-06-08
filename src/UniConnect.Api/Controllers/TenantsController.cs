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
    ITenantUserService tenantUserService,
    ITenantGeocodingSettingsService geocodingSettings,
    ITenantPlanningRulesService planningRules,
    ITenantDeliverySettingsService deliverySettings) : ControllerBase
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

    [HttpGet("me/geocoding")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantGeocodingSettingsDto>> GetMyGeocodingSettings(CancellationToken ct) =>
        Ok(await geocodingSettings.GetMySettingsAsync(ct));

    [HttpPut("me/geocoding")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantGeocodingSettingsDto>> UpdateMyGeocodingSettings(
        [FromBody] UpdateTenantGeocodingSettingsRequest request,
        CancellationToken ct) =>
        Ok(await geocodingSettings.UpdateMySettingsAsync(request, ct));

    [HttpPost("me/geocoding/test")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TestTenantGeocodingResultDto>> TestMyGeocodingSettings(
        [FromBody] TestTenantGeocodingRequest request,
        CancellationToken ct) =>
        Ok(await geocodingSettings.TestMyGeocodingAsync(request, ct));

    [HttpGet("me/planning-rules")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantPlanningRulesDto>> GetMyPlanningRules(CancellationToken ct) =>
        Ok(await planningRules.GetMyRulesAsync(ct));

    [HttpPut("me/planning-rules")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantPlanningRulesDto>> UpdateMyPlanningRules(
        [FromBody] UpdateTenantPlanningRulesRequest request,
        CancellationToken ct) =>
        Ok(await planningRules.UpdateMyRulesAsync(request, ct));

    [HttpPost("me/planning-rules/compile")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<CompileTenantPlanningRulesResultDto>> CompileMyPlanningRules(CancellationToken ct) =>
        Ok(await planningRules.CompileMyRulesAsync(ct));

    [HttpGet("me/delivery-settings")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantDeliverySettingsDto>> GetMyDeliverySettings(CancellationToken ct) =>
        Ok(await deliverySettings.GetMySettingsAsync(ct));

    [HttpPut("me/delivery-settings")]
    [Tags("Tenant — Settings")]
    public async Task<ActionResult<TenantDeliverySettingsDto>> UpdateMyDeliverySettings(
        [FromBody] UpdateTenantDeliverySettingsRequest request,
        CancellationToken ct) =>
        Ok(await deliverySettings.UpdateMySettingsAsync(request, ct));

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
