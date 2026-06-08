namespace UniConnect.RoutePlanning.Interfaces;

public sealed record PlanRunExplanationResult(
    string Explanation,
    bool UsedAi);

public interface IPlanRunExplainer
{
    Task<PlanRunExplanationResult> ExplainAsync(
        Guid tenantId,
        string planRunJson,
        string? policyJson,
        CancellationToken ct = default);
}
