namespace UniConnect.RoutePlanning.Models;

public sealed class PlanningPolicy
{
    public string Version { get; set; } = "1";

    public FleetPlanningPolicy Fleet { get; set; } = new();

    /// <summary>Driver overrides keyed by normalized display name.</summary>
    public Dictionary<string, DriverPlanningPolicy> Drivers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public FixedRoutesPlanningPolicy FixedRoutes { get; set; } = new();
}

public sealed class FleetPlanningPolicy
{
    /// <summary>driveMinutes, stopCount, or none.</summary>
    public string BalanceObjective { get; set; } = "driveMinutes";

    public int MaxImbalancePercent { get; set; } = 15;

    public int MaxStopsPerRoute { get; set; } = 25;

    /// <summary>When true, re-sequence and move or unassign orders that exceed assigned driver caps.</summary>
    public bool EnforceDriverCaps { get; set; }

    /// <summary>When true, multi-truck plans group stops by geography before sequencing.</summary>
    public bool UseGeographicClustering { get; set; } = true;
}

public sealed class DriverPlanningPolicy
{
    public int? MaxRouteMinutes { get; set; }

    /// <summary>HH:mm local — driver should return to depot by this time.</summary>
    public string? ReturnByTime { get; set; }
}

public sealed class FixedRoutesPlanningPolicy
{
    public bool AllowAddOnOrders { get; set; }

    /// <summary>manual — planner selects add-ons explicitly.</summary>
    public string AddOnMode { get; set; } = "manual";
}
