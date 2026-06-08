using UniConnect.RoutePlanning.Interfaces;

using UniConnect.RoutePlanning.Models;



namespace UniConnect.RoutePlanning.Routing;



/// <summary>

/// When truck balancing is enabled, splits orders across at least <paramref name="targetRouteCount"/> routes

/// instead of leaving Clarke–Wright merged into a single feasible mega-route.

/// </summary>

public static class BalancedRoutePartitioner

{

    public static bool ShouldApply(string? balanceObjective, int slotCount, int orderCount) =>

        slotCount > 1

        && orderCount > 1

        && (string.Equals(balanceObjective, "driveMinutes", StringComparison.OrdinalIgnoreCase)

            || string.Equals(balanceObjective, "stopCount", StringComparison.OrdinalIgnoreCase));



    public static IReadOnlyList<IReadOnlyList<OrderForPlanning>> EnsureBalancedPartitions(

        GeoPoint depot,

        string depotAddress,

        IReadOnlyList<IReadOnlyList<OrderForPlanning>> partitions,

        int targetRouteCount,

        string balanceObjective,

        int maxOrdersPerRoute,

        int? maxRouteMinutes,

        ITravelTimeMatrix matrix,

        int serviceMinutesPerStop,

        bool useGeographicClustering = true)

    {

        if (partitions.Count == 0)

            return partitions;



        var orders = partitions.SelectMany(p => p).ToList();

        if (orders.Count == 0)

            return partitions;



        var routeTarget = Math.Min(targetRouteCount, orders.Count);

        if (partitions.Count >= routeTarget)

            return partitions;



        return PartitionBalanced(

            depot,

            depotAddress,

            orders,

            routeTarget,

            balanceObjective,

            maxOrdersPerRoute,

            maxRouteMinutes,

            matrix,

            serviceMinutesPerStop,

            useGeographicClustering);

    }



    public static IReadOnlyList<IReadOnlyList<OrderForPlanning>> PartitionBalanced(

        GeoPoint depot,

        string depotAddress,

        IReadOnlyList<OrderForPlanning> orders,

        int targetRouteCount,

        string balanceObjective,

        int maxOrdersPerRoute,

        int? maxRouteMinutes,

        ITravelTimeMatrix matrix,

        int serviceMinutesPerStop,

        bool useGeographicClustering = true)

    {

        if (targetRouteCount <= 0 || orders.Count == 0)

            return [];



        if (targetRouteCount == 1)

            return [orders];



        if (useGeographicClustering)

        {

            return GeographicRouteClusterer.PartitionForRoutes(

                orders,

                targetRouteCount,

                depot,

                depotAddress,

                balanceObjective,

                maxOrdersPerRoute,

                maxRouteMinutes,

                matrix,

                serviceMinutesPerStop);

        }



        return PartitionGreedyBalanced(

            depot,

            depotAddress,

            orders,

            targetRouteCount,

            balanceObjective,

            maxOrdersPerRoute,

            maxRouteMinutes,

            matrix,

            serviceMinutesPerStop);

    }



    private static IReadOnlyList<IReadOnlyList<OrderForPlanning>> PartitionGreedyBalanced(

        GeoPoint depot,

        string depotAddress,

        IReadOnlyList<OrderForPlanning> orders,

        int targetRouteCount,

        string balanceObjective,

        int maxOrdersPerRoute,

        int? maxRouteMinutes,

        ITravelTimeMatrix matrix,

        int serviceMinutesPerStop)

    {

        var useDriveMinutes = string.Equals(balanceObjective, "driveMinutes", StringComparison.OrdinalIgnoreCase);

        var weights = orders.ToDictionary(

            o => o.Id,

            o => useDriveMinutes

                ? EstimateOrderRouteMinutes(o, depot, depotAddress, matrix, serviceMinutesPerStop)

                : 1);



        var bins = Enumerable.Range(0, targetRouteCount)

            .Select(_ => new List<OrderForPlanning>())

            .ToList();

        var loads = new int[targetRouteCount];



        foreach (var order in orders.OrderByDescending(o => weights[o.Id]))

        {

            var weight = weights[order.Id];

            var bestBin = SelectBin(bins, loads, weight, maxOrdersPerRoute, maxRouteMinutes);

            bins[bestBin].Add(order);

            loads[bestBin] += weight;

        }



        return bins

            .Where(b => b.Count > 0)

            .Select(b => (IReadOnlyList<OrderForPlanning>)b)

            .ToList();

    }



    private static int SelectBin(

        IReadOnlyList<List<OrderForPlanning>> bins,

        int[] loads,

        int weight,

        int maxOrdersPerRoute,

        int? maxRouteMinutes)

    {

        var best = -1;

        var bestLoad = int.MaxValue;



        for (var i = 0; i < bins.Count; i++)

        {

            if (bins[i].Count >= maxOrdersPerRoute)

                continue;

            if (maxRouteMinutes.HasValue && loads[i] + weight > maxRouteMinutes.Value)

                continue;



            if (loads[i] < bestLoad)

            {

                bestLoad = loads[i];

                best = i;

            }

        }



        if (best >= 0)

            return best;



        best = 0;

        bestLoad = int.MaxValue;

        for (var i = 0; i < bins.Count; i++)

        {

            if (loads[i] < bestLoad)

            {

                bestLoad = loads[i];

                best = i;

            }

        }



        return best;

    }



    private static int EstimateOrderRouteMinutes(

        OrderForPlanning order,

        GeoPoint depot,

        string depotAddress,

        ITravelTimeMatrix matrix,

        int serviceMinutesPerStop)

    {

        var depotPickup = PickupDeliveryRouteOptimizer.IsPickupAtDepot(order, depot, depotAddress);

        var toFirst = matrix.TravelMinutes(depot, depotPickup ? order.Delivery : order.Pickup);

        var within = depotPickup ? 0 : matrix.TravelMinutes(order.Pickup, order.Delivery);

        var toDepot = matrix.TravelMinutes(order.Delivery, depot);

        var service = (depotPickup ? 1 : 2) * serviceMinutesPerStop;

        return toFirst + within + toDepot + service;

    }

}


