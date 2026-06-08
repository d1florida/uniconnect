using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Routing;

public sealed record RouteRebalanceResult(
    IReadOnlyList<IReadOnlyList<OrderForPlanning>> Partitions,
    IReadOnlyList<OrderForPlanning> Unassigned);

/// <summary>
/// After stop sequencing, moves or unassigns orders so each route fits its driver cap.
/// </summary>
public static class RouteRebalancer
{
    private const int MaxIterations = 200;

    public static RouteRebalanceResult Rebalance(
        IReadOnlyList<IReadOnlyList<OrderForPlanning>> partitions,
        IReadOnlyList<int> routeCapMinutes,
        int maxOrdersPerRoute,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (partitions.Count == 0)
            return new RouteRebalanceResult([], []);

        var routes = partitions.Select(p => p.ToList()).ToList();
        var caps = routeCapMinutes.Count > 0
            ? routeCapMinutes.ToList()
            : Enumerable.Repeat(int.MaxValue, routes.Count).ToList();

        while (caps.Count < routes.Count)
            caps.Add(caps[^1]);

        var unassigned = new List<OrderForPlanning>();

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var changed = false;

            for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                var cap = caps[routeIndex];
                while (routes[routeIndex].Count > 0)
                {
                    var minutes = EstimateSequencedMinutes(
                        routes[routeIndex], depot, depotAddress, matrix, serviceMinutesPerStop);
                    if (minutes <= cap)
                        break;

                    var orderToMove = SelectOrderToRemove(
                        routes[routeIndex], depot, depotAddress, matrix, serviceMinutesPerStop, cap);
                    if (orderToMove is null)
                        break;

                    routes[routeIndex].Remove(orderToMove);

                    if (TryMoveToAnotherRoute(routes, orderToMove, routeIndex, caps, maxOrdersPerRoute,
                            depot, depotAddress, matrix, serviceMinutesPerStop))
                    {
                        changed = true;
                        continue;
                    }

                    unassigned.Add(orderToMove);
                    changed = true;
                }
            }

            if (!changed)
                break;
        }

        var nonEmpty = routes
            .Where(r => r.Count > 0)
            .Select(r => (IReadOnlyList<OrderForPlanning>)r)
            .ToList();

        return new RouteRebalanceResult(nonEmpty, unassigned);
    }

    private static bool TryMoveToAnotherRoute(
        List<List<OrderForPlanning>> routes,
        OrderForPlanning order,
        int sourceRouteIndex,
        IReadOnlyList<int> caps,
        int maxOrdersPerRoute,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        var bestRoute = -1;
        var bestScore = long.MaxValue;

        for (var target = 0; target < routes.Count; target++)
        {
            if (target == sourceRouteIndex)
                continue;
            if (routes[target].Count >= maxOrdersPerRoute)
                continue;

            var trial = routes[target].Append(order).ToList();
            var minutes = EstimateSequencedMinutes(trial, depot, depotAddress, matrix, serviceMinutesPerStop);
            if (minutes > caps[target])
                continue;

            var geoMinutes = routes[target].Count > 0
                ? routes[target].Min(o => matrix.TravelMinutes(order.Delivery, o.Delivery))
                : matrix.TravelMinutes(depot, order.Delivery);
            var score = (long)minutes * 10 + geoMinutes;

            if (score < bestScore)
            {
                bestScore = score;
                bestRoute = target;
            }
        }

        if (bestRoute < 0)
            return false;

        routes[bestRoute].Add(order);
        return true;
    }

    private static OrderForPlanning? SelectOrderToRemove(
        IReadOnlyList<OrderForPlanning> orders,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop,
        int targetCapMinutes)
    {
        if (orders.Count == 0)
            return null;

        if (orders.Count == 1)
            return orders[0];

        var current = EstimateSequencedMinutes(orders, depot, depotAddress, matrix, serviceMinutesPerStop);
        OrderForPlanning? bestOrder = null;
        var bestMinutes = current;

        foreach (var candidate in orders)
        {
            var remaining = orders.Where(o => o.Id != candidate.Id).ToList();
            var minutes = EstimateSequencedMinutes(remaining, depot, depotAddress, matrix, serviceMinutesPerStop);
            if (minutes <= targetCapMinutes)
                return candidate;

            if (minutes < bestMinutes)
            {
                bestMinutes = minutes;
                bestOrder = candidate;
            }
        }

        return bestOrder;
    }

    private static int EstimateSequencedMinutes(
        IReadOnlyList<OrderForPlanning> orders,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (orders.Count == 0)
            return 0;

        var rawStops = PickupDeliveryRouteOptimizer.BuildPlanningStops(orders, depot, depotAddress);
        var pickedUpAtDepot = PickupDeliveryRouteOptimizer.PickedUpAtDepotOrderIds(orders, depot, depotAddress);
        var optimized = PickupDeliveryRouteOptimizer.OptimizeStopSequence(
            depot,
            rawStops,
            matrix,
            pickedUpAtDepot,
            DriverSchedule.DefaultShiftStart,
            serviceMinutesPerStop);

        return PickupDeliveryRouteOptimizer.EstimateRouteMinutes(
            depot, optimized, matrix, serviceMinutesPerStop);
    }
}
