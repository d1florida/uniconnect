namespace UniConnect.Tenant.DTOs;

public record TenantPlanningRulesDto(
    string Markdown,
    string? CompiledPolicyJson,
    DateTime? CompiledAt,
    IReadOnlyList<string> CompileWarnings,
    DateTime UpdatedAt);

public record UpdateTenantPlanningRulesRequest(string Markdown);

public record CompileTenantPlanningRulesResultDto(
    string? CompiledPolicyJson,
    IReadOnlyList<string> Warnings,
    bool UsedAi);
