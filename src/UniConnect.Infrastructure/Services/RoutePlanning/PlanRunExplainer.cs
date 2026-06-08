using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Options;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public sealed class PlanRunExplainer(
    IOptions<PlanningRulesOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<PlanRunExplainer> logger) : IPlanRunExplainer
{
    public async Task<PlanRunExplanationResult> ExplainAsync(
        Guid tenantId,
        string planRunJson,
        string? policyJson,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.EnablePlanExplanation || string.IsNullOrWhiteSpace(opts.OpenAiApiKey))
        {
            return new PlanRunExplanationResult(BuildDeterministicSummary(planRunJson, policyJson), UsedAi: false);
        }

        try
        {
            var explanation = await ExplainWithAiAsync(planRunJson, policyJson, ct);
            return new PlanRunExplanationResult(explanation, UsedAi: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI plan explanation failed for tenant {TenantId}", tenantId);
            return new PlanRunExplanationResult(BuildDeterministicSummary(planRunJson, policyJson), UsedAi: false);
        }
    }

    private async Task<string> ExplainWithAiAsync(string planRunJson, string? policyJson, CancellationToken ct)
    {
        var opts = options.Value;
        var client = httpClientFactory.CreateClient(nameof(PlanRunExplainer));
        client.BaseAddress = new Uri(opts.OpenAiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.OpenAiApiKey);

        var body = new
        {
            model = opts.OpenAiModel,
            temperature = 0.2,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = """
                        You explain delivery route plans to dispatchers in plain English.
                        Be concise (3-6 sentences). Mention driver caps, truck balance, unassigned orders, and window warnings if present.
                        Do not invent data not in the JSON.
                        """
                },
                new
                {
                    role = "user",
                    content = $"Planning policy JSON:\n{policyJson ?? "{}"}\n\nPlan run JSON:\n{planRunJson}"
                }
            }
        };

        using var response = await client.PostAsync(
            "chat/completions",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? BuildDeterministicSummary(planRunJson, policyJson);
    }

    private static string BuildDeterministicSummary(string planRunJson, string? policyJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(planRunJson);
            var root = doc.RootElement;
            var proposals = root.TryGetProperty("proposals", out var p) ? p.GetArrayLength() : 0;
            var planned = root.TryGetProperty("ordersPlanned", out var op) ? op.GetInt32() : 0;
            var unassigned = root.TryGetProperty("ordersUnassigned", out var ou) ? ou.GetInt32() : 0;
            var parts = new List<string>
            {
                $"Plan proposes {proposals} route(s) covering {planned} order(s)."
            };
            if (unassigned > 0)
                parts.Add($"{unassigned} order(s) could not be assigned.");

            if (!string.IsNullOrWhiteSpace(policyJson))
                parts.Add("Tenant planning rules were applied.");

            return string.Join(' ', parts);
        }
        catch
        {
            return "Route plan completed. Review proposals for driver assignments and stop order.";
        }
    }
}
