namespace UniConnect.RoutePlanning.Routing;

/// <summary>Assigns route partitions to vehicle/driver slots with optional drive-minute balancing.</summary>
public static class RouteAssignmentBalancer
{
    public static IReadOnlyList<int> AssignPartitionsToSlots(
        IReadOnlyList<int> partitionWeights,
        int slotCount,
        string balanceObjective)
    {
        if (slotCount <= 0 || partitionWeights.Count == 0)
            return [];

        if (!string.Equals(balanceObjective, "driveMinutes", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(balanceObjective, "stopCount", StringComparison.OrdinalIgnoreCase))
        {
            return Enumerable.Range(0, Math.Min(partitionWeights.Count, slotCount)).ToList();
        }

        var slotLoads = new int[slotCount];
        var assignments = new int[partitionWeights.Count];

        var ordered = partitionWeights
            .Select((weight, index) => (weight, index))
            .OrderByDescending(x => x.weight)
            .ToList();

        foreach (var (weight, partitionIndex) in ordered)
        {
            var slot = 0;
            var bestLoad = int.MaxValue;
            for (var s = 0; s < slotCount; s++)
            {
                if (slotLoads[s] < bestLoad)
                {
                    bestLoad = slotLoads[s];
                    slot = s;
                }
            }

            assignments[partitionIndex] = slot;
            slotLoads[slot] += weight;
        }

        return assignments;
    }

    public static IReadOnlyList<int> AssignPartitionsToDrivers(
        IReadOnlyList<int> partitionMinutes,
        IReadOnlyList<int> driverPerRouteCaps,
        IReadOnlyList<int> driverDayAvailableMinutes,
        string balanceObjective,
        bool allowMultipleRoutesPerDriverPerDay)
    {
        if (driverPerRouteCaps.Count == 0 || partitionMinutes.Count == 0)
            return [];

        var oneDriverPerRoute = !allowMultipleRoutesPerDriverPerDay
            && partitionMinutes.Count <= driverPerRouteCaps.Count;
        var driverLoads = new int[driverPerRouteCaps.Count];
        var driverUsed = new bool[driverPerRouteCaps.Count];
        var result = new int[partitionMinutes.Count];

        var ordered = partitionMinutes
            .Select((minutes, index) => (minutes, index))
            .OrderByDescending(x => x.minutes)
            .ToList();

        const int overCapPenalty = 1_000_000;

        foreach (var (minutes, partitionIndex) in ordered)
        {
            var bestDriver = -1;
            var bestScore = long.MaxValue;

            for (var d = 0; d < driverPerRouteCaps.Count; d++)
            {
                if (oneDriverPerRoute && driverUsed[d])
                    continue;

                var score = (long)driverLoads[d] + minutes;
                if (minutes > driverPerRouteCaps[d])
                    score += overCapPenalty;

                if (allowMultipleRoutesPerDriverPerDay
                    && driverLoads[d] + minutes > driverDayAvailableMinutes[d])
                {
                    score += overCapPenalty;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestDriver = d;
                }
            }

            if (bestDriver < 0)
            {
                bestDriver = Array.FindIndex(driverUsed, used => !used);
                if (bestDriver < 0)
                    bestDriver = 0;
            }

            result[partitionIndex] = bestDriver;
            driverLoads[bestDriver] += minutes;
            if (oneDriverPerRoute)
                driverUsed[bestDriver] = true;
        }

        return result;
    }
}
