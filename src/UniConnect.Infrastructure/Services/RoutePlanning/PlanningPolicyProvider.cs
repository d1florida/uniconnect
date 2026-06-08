using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniConnect.Infrastructure.Data;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public sealed class PlanningPolicyProvider(AppDbContext db) : IPlanningPolicyProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<PlanningPolicy> GetPolicyForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var rules = await db.TenantPlanningRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId, ct);

        if (rules is null || string.IsNullOrWhiteSpace(rules.CompiledPolicyJson))
            return PlanningPolicyDefaults.CreateDefault();

        try
        {
            var policy = JsonSerializer.Deserialize<PlanningPolicy>(rules.CompiledPolicyJson, JsonOptions)
                ?? PlanningPolicyDefaults.CreateDefault();
            if (!rules.CompiledPolicyJson.Contains("useGeographicClustering", StringComparison.OrdinalIgnoreCase))
                policy.Fleet.UseGeographicClustering = true;
            return policy;
        }
        catch
        {
            return PlanningPolicyDefaults.CreateDefault();
        }
    }
}
