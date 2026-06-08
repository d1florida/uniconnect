using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.RoutePlanning.Options;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public sealed class PlanningPolicyCompiler(
    IOptions<PlanningRulesOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<PlanningPolicyCompiler> logger) : IPlanningPolicyCompiler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<PlanningPolicyCompileResult> CompileAsync(string markdown, CancellationToken ct = default)
    {
        var baseline = PlanningPolicyMarkdownParser.Parse(markdown);
        var opts = options.Value;

        if (!opts.EnableAiCompiler || string.IsNullOrWhiteSpace(opts.OpenAiApiKey))
            return baseline;

        try
        {
            var aiPolicy = await CompileWithAiAsync(markdown, ct);
            var merged = MergePolicies(baseline.Policy, aiPolicy);
            var warnings = baseline.Warnings.ToList();
            warnings.Add("Policy refined with AI compiler.");
            return new PlanningPolicyCompileResult(merged, warnings, UsedAi: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI planning policy compile failed; using deterministic parser.");
            var warnings = baseline.Warnings.ToList();
            warnings.Add("AI compiler unavailable — saved deterministic parse only.");
            return new PlanningPolicyCompileResult(baseline.Policy, warnings, UsedAi: false);
        }
    }

    private async Task<PlanningPolicy> CompileWithAiAsync(string markdown, CancellationToken ct)
    {
        var opts = options.Value;
        var client = httpClientFactory.CreateClient(nameof(PlanningPolicyCompiler));
        client.BaseAddress = new Uri(opts.OpenAiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.OpenAiApiKey);

        var schemaExample = JsonSerializer.Serialize(PlanningPolicyDefaults.CreateDefault(), JsonOptions);
        var systemPrompt = """
            You convert tenant delivery planning rules (markdown) into JSON matching this schema.
            Output ONLY valid JSON, no markdown fences.
            Use balanceObjective: driveMinutes | stopCount | none.
            Driver keys are display names exactly as written in the rules.
            Times use 24h HH:mm.
            """;

        var body = new
        {
            model = opts.OpenAiModel,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Schema example:\n{schemaExample}\n\nTenant rules:\n{markdown}" }
            }
        };

        using var response = await client.PostAsync(
            "chat/completions",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? throw new InvalidOperationException("Empty AI response.");

        return JsonSerializer.Deserialize<PlanningPolicy>(content, JsonOptions)
            ?? PlanningPolicyDefaults.CreateDefault();
    }

    private static PlanningPolicy MergePolicies(PlanningPolicy baseline, PlanningPolicy ai)
    {
        var merged = PlanningPolicyDefaults.CreateDefault();
        merged.Version = ai.Version ?? baseline.Version;

        merged.Fleet.BalanceObjective = Prefer(ai.Fleet.BalanceObjective, baseline.Fleet.BalanceObjective);
        merged.Fleet.MaxImbalancePercent = ai.Fleet.MaxImbalancePercent > 0
            ? ai.Fleet.MaxImbalancePercent
            : baseline.Fleet.MaxImbalancePercent;
        merged.Fleet.MaxStopsPerRoute = ai.Fleet.MaxStopsPerRoute > 0
            ? ai.Fleet.MaxStopsPerRoute
            : baseline.Fleet.MaxStopsPerRoute;
        merged.Fleet.EnforceDriverCaps = baseline.Fleet.EnforceDriverCaps;
        merged.Fleet.UseGeographicClustering = baseline.Fleet.UseGeographicClustering;

        merged.FixedRoutes.AllowAddOnOrders = ai.FixedRoutes.AllowAddOnOrders || baseline.FixedRoutes.AllowAddOnOrders;
        merged.FixedRoutes.AddOnMode = Prefer(ai.FixedRoutes.AddOnMode, baseline.FixedRoutes.AddOnMode);

        foreach (var pair in baseline.Drivers)
            merged.Drivers[pair.Key] = pair.Value;
        foreach (var pair in ai.Drivers)
        {
            if (!merged.Drivers.TryGetValue(pair.Key, out var existing))
            {
                merged.Drivers[pair.Key] = pair.Value;
                continue;
            }

            existing.MaxRouteMinutes ??= pair.Value.MaxRouteMinutes;
            existing.ReturnByTime ??= pair.Value.ReturnByTime;
        }

        return merged;
    }

    private static string Prefer(string? primary, string fallback) =>
        string.IsNullOrWhiteSpace(primary) ? fallback : primary.Trim();
}
