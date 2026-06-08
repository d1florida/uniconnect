using System.Globalization;
using System.Text.RegularExpressions;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

/// <summary>Deterministic markdown → policy parser (always runs; AI may refine on top).</summary>
public static class PlanningPolicyMarkdownParser
{
    private static readonly Regex DriverHeadingRegex = new(
        @"^\s*(?:[-*]\s*)?\*\*(?<name>[^*]+)\*\*\s*:?\s*(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HourRegex = new(
        @"(?<value>\d+(?:\.\d+)?)\s*(?:-|–)?\s*(?:hour|hr)s?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MinuteRegex = new(
        @"(?<value>\d+)\s*(?:-|–)?\s*(?:minute|min)s?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReturnByRegex = new(
        @"(?:back|return(?:\s+to\s+depot)?)\s+by\s+(?<time>\d{1,2}(?::\d{2})?\s*(?:am|pm)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static PlanningPolicyCompileResult Parse(string markdown)
    {
        var warnings = new List<string>();
        var policy = PlanningPolicyDefaults.CreateDefault();

        if (string.IsNullOrWhiteSpace(markdown))
        {
            warnings.Add("Rules document is empty — using default planning policy.");
            return new PlanningPolicyCompileResult(policy, warnings, UsedAi: false);
        }

        var lines = markdown.Split('\n');
        var section = string.Empty;
        var sawDriversSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                section = NormalizeSection(line);
                if (section is "drivers")
                    sawDriversSection = true;
                continue;
            }

            if (line.Length == 0)
                continue;

            if (section is "fleet" or "")
                ApplyFleetLine(line, policy.Fleet, warnings);

            if (section is "fixedroutes" or "fixed")
                ApplyFixedRoutesLine(line, policy.FixedRoutes, warnings);
        }

        if (sawDriversSection)
        {
            warnings.Add(
                "The ## Drivers section is ignored — set weekly schedules and route caps under Delivery → Drivers.");
        }

        NormalizePolicy(policy, warnings);
        return new PlanningPolicyCompileResult(policy, warnings, UsedAi: false);
    }

    private static string NormalizeSection(string heading)
    {
        var text = heading.TrimStart('#').Trim().ToLowerInvariant();
        text = text.Replace(" ", string.Empty);
        return text;
    }

    private static void ApplyFleetLine(string line, FleetPlanningPolicy fleet, List<string> warnings)
    {
        var lower = line.ToLowerInvariant();

        if (lower.Contains("balance", StringComparison.Ordinal) &&
            (lower.Contains("truck", StringComparison.Ordinal) ||
             lower.Contains("vehicle", StringComparison.Ordinal) ||
             lower.Contains("equally", StringComparison.Ordinal) ||
             lower.Contains("equal", StringComparison.Ordinal)))
        {
            if (lower.Contains("stop", StringComparison.Ordinal))
                fleet.BalanceObjective = "stopCount";
            else
                fleet.BalanceObjective = "driveMinutes";
        }

        if (lower.Contains("no balance", StringComparison.Ordinal) ||
            lower.Contains("don't balance", StringComparison.Ordinal))
        {
            fleet.BalanceObjective = "none";
        }

        var stopMatch = Regex.Match(line, @"max(?:imum)?\s+(\d+)\s+stops?", RegexOptions.IgnoreCase);
        if (stopMatch.Success && int.TryParse(stopMatch.Groups[1].Value, out var maxStops))
            fleet.MaxStopsPerRoute = maxStops;

        var imbalanceMatch = Regex.Match(line, @"(\d+)\s*%", RegexOptions.IgnoreCase);
        if (imbalanceMatch.Success && lower.Contains("imbalance", StringComparison.Ordinal) &&
            int.TryParse(imbalanceMatch.Groups[1].Value, out var pct))
        {
            fleet.MaxImbalancePercent = pct;
        }

        if (lower.Contains("enforce", StringComparison.Ordinal) &&
            (lower.Contains("cap", StringComparison.Ordinal) || lower.Contains("driver", StringComparison.Ordinal)))
        {
            fleet.EnforceDriverCaps = !lower.Contains("do not enforce", StringComparison.Ordinal) &&
                                      !lower.Contains("don't enforce", StringComparison.Ordinal) &&
                                      !lower.Contains("soft cap", StringComparison.Ordinal);
        }

        if (lower.Contains("hard cap", StringComparison.Ordinal) || lower.Contains("hard limit", StringComparison.Ordinal))
            fleet.EnforceDriverCaps = true;

        if (lower.Contains("cluster", StringComparison.Ordinal) &&
            (lower.Contains("geograph", StringComparison.Ordinal) ||
             lower.Contains("region", StringComparison.Ordinal) ||
             lower.Contains("territor", StringComparison.Ordinal)))
        {
            fleet.UseGeographicClustering = !lower.Contains("do not cluster", StringComparison.Ordinal) &&
                                            !lower.Contains("don't cluster", StringComparison.Ordinal) &&
                                            !lower.Contains("no cluster", StringComparison.Ordinal) &&
                                            !lower.Contains("disable cluster", StringComparison.Ordinal);
        }
    }

    private static void ApplyDriverLine(string line, PlanningPolicy policy, List<string> warnings)
    {
        var match = DriverHeadingRegex.Match(line);
        if (!match.Success)
            return;

        var name = match.Groups["name"].Value.Trim();
        var rest = match.Groups["rest"].Value;
        if (string.IsNullOrWhiteSpace(name))
            return;

        var driverPolicy = policy.Drivers.TryGetValue(name, out var existing)
            ? existing
            : new DriverPlanningPolicy();

        ApplyDriverConstraints(rest, driverPolicy, warnings);
        policy.Drivers[name] = driverPolicy;
    }

    private static void ApplyDriverConstraints(string text, DriverPlanningPolicy driver, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var hourMatch = HourRegex.Match(text);
        if (hourMatch.Success &&
            double.TryParse(hourMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours))
        {
            if (text.Contains("max", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("route", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("run", StringComparison.OrdinalIgnoreCase))
            {
                driver.MaxRouteMinutes = (int)Math.Round(hours * 60);
            }
        }

        var minuteMatch = MinuteRegex.Match(text);
        if (minuteMatch.Success && int.TryParse(minuteMatch.Groups["value"].Value, out var minutes) &&
            text.Contains("max", StringComparison.OrdinalIgnoreCase))
        {
            driver.MaxRouteMinutes = minutes;
        }

        var returnMatch = ReturnByRegex.Match(text);
        if (returnMatch.Success)
        {
            if (TryParseTime(returnMatch.Groups["time"].Value.Trim(), out var formatted))
                driver.ReturnByTime = formatted;
            else
                warnings.Add($"Could not parse return-by time in: {text}");
        }
    }

    private static void ApplyFixedRoutesLine(string line, FixedRoutesPlanningPolicy fixedRoutes, List<string> warnings)
    {
        var lower = line.ToLowerInvariant();
        if (lower.Contains("add-on", StringComparison.Ordinal) ||
            lower.Contains("addon", StringComparison.Ordinal) ||
            lower.Contains("extra stop", StringComparison.Ordinal) ||
            lower.Contains("fill-in", StringComparison.Ordinal) ||
            lower.Contains("fill in", StringComparison.Ordinal))
        {
            fixedRoutes.AllowAddOnOrders = !lower.Contains("not allow", StringComparison.Ordinal) &&
                                           !lower.Contains("no add", StringComparison.Ordinal) &&
                                           !lower.Contains("disallow", StringComparison.Ordinal);
            fixedRoutes.AddOnMode = "manual";
        }
    }

    private static void NormalizePolicy(PlanningPolicy policy, List<string> warnings)
    {
        if (policy.Fleet.MaxStopsPerRoute <= 0)
        {
            policy.Fleet.MaxStopsPerRoute = 25;
            warnings.Add("Max stops per route was invalid — reset to 25.");
        }

        if (policy.Fleet.MaxImbalancePercent <= 0)
            policy.Fleet.MaxImbalancePercent = 15;

        var objective = policy.Fleet.BalanceObjective?.Trim() ?? "driveMinutes";
        if (objective is not ("driveMinutes" or "stopCount" or "none"))
        {
            warnings.Add($"Unknown balance objective '{objective}' — using driveMinutes.");
            policy.Fleet.BalanceObjective = "driveMinutes";
        }
    }

    private static bool TryParseTime(string raw, out string formatted)
    {
        formatted = string.Empty;
        var normalized = raw.ToUpperInvariant().Replace(" ", string.Empty);
        if (TimeOnly.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            formatted = time.ToString("HH:mm", CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }
}

public static class PlanningPolicyDefaults
{
    public static PlanningPolicy CreateDefault() => new()
    {
        Version = "1",
        Fleet = new FleetPlanningPolicy
        {
            BalanceObjective = "driveMinutes",
            MaxImbalancePercent = 15,
            MaxStopsPerRoute = 25,
            UseGeographicClustering = true
        },
        FixedRoutes = new FixedRoutesPlanningPolicy
        {
            AllowAddOnOrders = false,
            AddOnMode = "manual"
        }
    };

    public const string DefaultMarkdown = """
        # Planning rules

        ## Fleet
        - Balance routes across active trucks by estimated drive minutes.
        - Cluster stops by geography when multiple trucks run (reduces cross-region miles).
        - Maximum 25 stops per route.
        - Enforce driver route caps — overflow orders stay unassigned.

        ## Fixed routes
        - Held orders wait for their fixed route day.
        - Same-day add-on orders may be included when the planner selects them.
        """;
}
