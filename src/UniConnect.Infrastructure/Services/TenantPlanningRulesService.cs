using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Services.RoutePlanning;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.Tenant.DTOs;
using UniConnect.Tenant.Entities;
using UniConnect.Tenant.Enums;
using UniConnect.Tenant.Interfaces;

namespace UniConnect.Infrastructure.Services;

public sealed class TenantPlanningRulesService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPlanningPolicyCompiler compiler) : ITenantPlanningRulesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<TenantPlanningRulesDto> GetMyRulesAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantAdminWithRoutePlanning();
        var entity = await GetOrCreateAsync(tenantId, ct);
        return Map(entity);
    }

    public async Task<TenantPlanningRulesDto> UpdateMyRulesAsync(
        UpdateTenantPlanningRulesRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantAdminWithRoutePlanning();
        var entity = await GetOrCreateAsync(tenantId, ct);
        entity.Markdown = request.Markdown ?? string.Empty;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = currentUser.UserId;

        var compiled = await compiler.CompileAsync(entity.Markdown, ct);
        entity.CompiledPolicyJson = JsonSerializer.Serialize(compiled.Policy, JsonOptions);
        entity.CompiledAt = DateTime.UtcNow;
        entity.CompileWarningsJson = JsonSerializer.Serialize(compiled.Warnings, JsonOptions);

        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<CompileTenantPlanningRulesResultDto> CompileMyRulesAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantAdminWithRoutePlanning();
        var entity = await GetOrCreateAsync(tenantId, ct);
        var compiled = await compiler.CompileAsync(entity.Markdown, ct);

        entity.CompiledPolicyJson = JsonSerializer.Serialize(compiled.Policy, JsonOptions);
        entity.CompiledAt = DateTime.UtcNow;
        entity.CompileWarningsJson = JsonSerializer.Serialize(compiled.Warnings, JsonOptions);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = currentUser.UserId;
        await db.SaveChangesAsync(ct);

        return new CompileTenantPlanningRulesResultDto(
            entity.CompiledPolicyJson,
            compiled.Warnings,
            compiled.UsedAi);
    }

    private async Task<TenantPlanningRules> GetOrCreateAsync(Guid tenantId, CancellationToken ct)
    {
        var entity = await db.TenantPlanningRules.FirstOrDefaultAsync(r => r.TenantId == tenantId, ct);
        if (entity is null)
        {
            entity = new TenantPlanningRules
            {
                TenantId = tenantId,
                Markdown = PlanningPolicyDefaults.DefaultMarkdown,
                UpdatedAt = DateTime.UtcNow
            };
            db.TenantPlanningRules.Add(entity);
        }

        if (string.IsNullOrWhiteSpace(entity.CompiledPolicyJson))
        {
            var compiled = await compiler.CompileAsync(entity.Markdown, ct);
            entity.CompiledPolicyJson = JsonSerializer.Serialize(compiled.Policy, JsonOptions);
            entity.CompiledAt = DateTime.UtcNow;
            entity.CompileWarningsJson = JsonSerializer.Serialize(compiled.Warnings, JsonOptions);
            await db.SaveChangesAsync(ct);
        }

        return entity;
    }

    private static TenantPlanningRulesDto Map(TenantPlanningRules entity)
    {
        IReadOnlyList<string> warnings = [];
        if (!string.IsNullOrWhiteSpace(entity.CompileWarningsJson))
        {
            try
            {
                warnings = JsonSerializer.Deserialize<List<string>>(entity.CompileWarningsJson) ?? [];
            }
            catch
            {
                warnings = [];
            }
        }

        return new TenantPlanningRulesDto(
            entity.Markdown,
            entity.CompiledPolicyJson,
            entity.CompiledAt,
            warnings,
            entity.UpdatedAt);
    }

    private Guid RequireTenantAdminWithRoutePlanning()
    {
        currentUser.EnsureModule(ProductModule.RoutePlanning);
        currentUser.EnsureTenantAdmin();
        if (currentUser.IsApiKeyAuth)
            throw new InvalidOperationException("API keys cannot manage planning rules.");
        return currentUser.TenantId
            ?? throw new InvalidOperationException("Tenant operator account required.");
    }
}
