using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Routing;

internal static class ClarkeWrightPartitioner
{
    private sealed class MutableRoute(List<OrderForPlanning> orders)
    {
        public List<OrderForPlanning> Orders { get; } = orders;
    }

    private readonly record struct OrderEndpoints(
        GeoPoint FirstStop,
        GeoPoint LastStop,
        int DepotToFirstMinutes,
        int FirstToLastMinutes,
        int LastToDepotMinutes);

    public static IReadOnlyList<IReadOnlyList<OrderForPlanning>> Partition(
        GeoPoint depot,
        string depotAddress,
        IReadOnlyList<OrderForPlanning> orders,
        int vehicleCount,
        int maxOrdersPerRoute,
        ITravelTimeMatrix matrix,
        int? maxRouteMinutes = null,
        int serviceMinutesPerStop = 5)
    {
        if (vehicleCount <= 0 || maxOrdersPerRoute <= 0 || orders.Count == 0)
            return [];

        var endpoints = orders.ToDictionary(
            o => o.Id,
            o => GetEndpoints(o, depot, depotAddress, matrix));

        var routes = orders.Select(o => new MutableRoute([o])).ToList();

        var savings = new List<(double Saving, int I, int J)>();
        for (var i = 0; i < orders.Count; i++)
        {
            for (var j = i + 1; j < orders.Count; j++)
            {
                var left = endpoints[orders[i].Id];
                var right = endpoints[orders[j].Id];
                var saving = left.DepotToFirstMinutes
                    + right.DepotToFirstMinutes
                    - matrix.TravelMinutes(left.LastStop, right.FirstStop);
                savings.Add((saving, i, j));
            }
        }

        savings.Sort((a, b) => b.Saving.CompareTo(a.Saving));

        foreach (var (saving, i, j) in savings)
        {
            if (saving <= 0)
                break;

            var routeLeft = FindRouteIndex(routes, orders[i].Id);
            var routeRight = FindRouteIndex(routes, orders[j].Id);
            if (routeLeft < 0 || routeRight < 0 || routeLeft == routeRight)
                continue;

            if (!TryMergeRoutes(
                    routes[routeLeft],
                    routes[routeRight],
                    maxOrdersPerRoute,
                    maxRouteMinutes,
                    serviceMinutesPerStop,
                    depot,
                    depotAddress,
                    endpoints,
                    matrix,
                    requireSavings: true,
                    out var merged))
                continue;

            routes[routeLeft] = merged;
            routes.RemoveAt(routeRight);
        }

        while (routes.Count > vehicleCount)
        {
            var removeAt = FindRouteToRemoveOrMerge(
                routes, maxOrdersPerRoute, maxRouteMinutes, serviceMinutesPerStop, depot, depotAddress, endpoints, matrix);
            routes.RemoveAt(removeAt);
        }

        routes.Sort((a, b) => RouteTotalMinutes(b, depot, depotAddress, endpoints, matrix, serviceMinutesPerStop).CompareTo(
            RouteTotalMinutes(a, depot, depotAddress, endpoints, matrix, serviceMinutesPerStop)));

        return routes
            .Select(r => (IReadOnlyList<OrderForPlanning>)r.Orders)
            .ToList();
    }

    private static int FindRouteToRemoveOrMerge(
        List<MutableRoute> routes,
        int maxOrdersPerRoute,
        int? maxRouteMinutes,
        int serviceMinutesPerStop,
        GeoPoint depot,
        string depotAddress,
        Dictionary<Guid, OrderEndpoints> endpoints,
        ITravelTimeMatrix matrix)
    {
        var bestPair = (-1, -1);
        var bestCost = int.MaxValue;

        for (var i = 0; i < routes.Count; i++)
        {
            for (var j = i + 1; j < routes.Count; j++)
            {
                if (routes[i].Orders.Count + routes[j].Orders.Count > maxOrdersPerRoute)
                    continue;

                if (!TryMergeRoutes(
                        routes[i],
                        routes[j],
                        maxOrdersPerRoute,
                        maxRouteMinutes,
                        serviceMinutesPerStop,
                        depot,
                        depotAddress,
                        endpoints,
                        matrix,
                        requireSavings: false,
                        out var merged))
                    continue;

                var cost = RouteTotalMinutes(merged!.Orders, depot, depotAddress, endpoints, matrix, serviceMinutesPerStop);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestPair = (i, j);
                }
            }
        }

        if (bestPair != (-1, -1))
        {
            var (left, right) = bestPair;
            TryMergeRoutes(
                routes[left],
                routes[right],
                maxOrdersPerRoute,
                maxRouteMinutes,
                serviceMinutesPerStop,
                depot,
                depotAddress,
                endpoints,
                matrix,
                requireSavings: false,
                out var combined);
            routes[left] = combined!;
            return right;
        }

        var smallest = 0;
        for (var i = 1; i < routes.Count; i++)
        {
            if (routes[i].Orders.Count < routes[smallest].Orders.Count)
                smallest = i;
        }

        return smallest;
    }

    private static bool TryMergeRoutes(
        MutableRoute left,
        MutableRoute right,
        int maxOrdersPerRoute,
        int? maxRouteMinutes,
        int serviceMinutesPerStop,
        GeoPoint depot,
        string depotAddress,
        Dictionary<Guid, OrderEndpoints> endpoints,
        ITravelTimeMatrix matrix,
        bool requireSavings,
        out MutableRoute? merged)
    {
        merged = null;
        if (left.Orders.Count + right.Orders.Count > maxOrdersPerRoute)
            return false;

        var candidates = new[]
        {
            left.Orders.Concat(right.Orders).ToList(),
            left.Orders.Concat(right.Orders.AsEnumerable().Reverse()).ToList(),
            left.Orders.AsEnumerable().Reverse().Concat(right.Orders).ToList(),
            right.Orders.Concat(left.Orders).ToList(),
        };

        var before = RouteTotalMinutes(left, depot, depotAddress, endpoints, matrix, serviceMinutesPerStop)
            + RouteTotalMinutes(right, depot, depotAddress, endpoints, matrix, serviceMinutesPerStop);

        var bestOrders = candidates[0];
        var bestCost = RouteTotalMinutes(bestOrders, depot, depotAddress, endpoints, matrix, serviceMinutesPerStop);
        foreach (var candidate in candidates.Skip(1))
        {
            var cost = RouteTotalMinutes(candidate, depot, depotAddress, endpoints, matrix, serviceMinutesPerStop);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestOrders = candidate;
            }
        }

        if (maxRouteMinutes.HasValue && bestCost > maxRouteMinutes.Value)
            return false;

        if (requireSavings && bestCost >= before)
            return false;

        merged = new MutableRoute(bestOrders);
        return true;
    }

    private static int RouteTotalMinutes(
        MutableRoute route,
        GeoPoint depot,
        string depotAddress,
        Dictionary<Guid, OrderEndpoints> endpoints,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop) =>
        RouteTotalMinutes(route.Orders, depot, depotAddress, endpoints, matrix, serviceMinutesPerStop);

    private static int RouteTotalMinutes(
        IReadOnlyList<OrderForPlanning> orders,
        GeoPoint depot,
        string depotAddress,
        Dictionary<Guid, OrderEndpoints> endpoints,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (orders.Count == 0)
            return 0;

        var first = endpoints[orders[0].Id];
        var total = first.DepotToFirstMinutes + first.FirstToLastMinutes;

        for (var i = 1; i < orders.Count; i++)
        {
            var previous = endpoints[orders[i - 1].Id];
            var current = endpoints[orders[i].Id];
            total += matrix.TravelMinutes(previous.LastStop, current.FirstStop);
            total += current.FirstToLastMinutes;
        }

        var last = endpoints[orders[^1].Id];
        total += last.LastToDepotMinutes;
        total += EstimateServiceMinutes(orders, depot, depotAddress, serviceMinutesPerStop);
        return total;
    }

    private static int EstimateServiceMinutes(
        IReadOnlyList<OrderForPlanning> orders,
        GeoPoint depot,
        string depotAddress,
        int serviceMinutesPerStop)
    {
        var stops = 0;
        foreach (var order in orders)
        {
            stops += PickupDeliveryRouteOptimizer.IsPickupAtDepot(order, depot, depotAddress) ? 1 : 2;
        }

        return stops * serviceMinutesPerStop;
    }

    private static OrderEndpoints GetEndpoints(
        OrderForPlanning order,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix)
    {
        var depotPickup = PickupDeliveryRouteOptimizer.IsPickupAtDepot(order, depot, depotAddress);
        var first = depotPickup ? order.Delivery : order.Pickup;
        var last = order.Delivery;
        return new OrderEndpoints(
            first,
            last,
            matrix.TravelMinutes(depot, first),
            depotPickup ? 0 : matrix.TravelMinutes(order.Pickup, order.Delivery),
            matrix.TravelMinutes(last, depot));
    }

    private static int FindRouteIndex(List<MutableRoute> routes, Guid orderId)
    {
        for (var i = 0; i < routes.Count; i++)
        {
            if (routes[i].Orders.Any(o => o.Id == orderId))
                return i;
        }

        return -1;
    }
}
