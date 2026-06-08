using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Interfaces;

public sealed record PlanningPolicyCompileResult(
    PlanningPolicy Policy,
    IReadOnlyList<string> Warnings,
    bool UsedAi);

public interface IPlanningPolicyCompiler
{
    Task<PlanningPolicyCompileResult> CompileAsync(string markdown, CancellationToken ct = default);
}
