using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Routing;

public sealed record PlanningStop(
    Guid? OrderId,
    string StopType,
    string Address,
    GeoPoint Location,
    string? RecipientName,
    string? ParcelDescription,
    Guid? RouteStopId = null);

public sealed record OrderForPlanning(
    Guid Id,
    string PickupAddress,
    string DeliveryAddress,
    GeoPoint Pickup,
    GeoPoint Delivery,
    string RecipientName,
    string ParcelDescription);

public static class PickupDeliveryRouteOptimizer
{
    public static IReadOnlyList<IReadOnlyList<OrderForPlanning>> PartitionOrdersAcrossVehicles(
        GeoPoint depot,
        string depotAddress,
        IReadOnlyList<OrderForPlanning> orders,
        int vehicleCount,
        int maxOrdersPerRoute,
        ITravelTimeMatrix matrix)
    {
        if (vehicleCount <= 0 || maxOrdersPerRoute <= 0 || orders.Count == 0)
            return [];

        var remaining = orders.ToList();
        var routes = new List<IReadOnlyList<OrderForPlanning>>();

        for (var v = 0; v < vehicleCount && remaining.Count > 0; v++)
        {
            var route = new List<OrderForPlanning>();
            var current = depot;

            while (route.Count < maxOrdersPerRoute && remaining.Count > 0)
            {
                var next = remaining
                    .OrderBy(o => matrix.TravelMinutes(
                        current,
                        IsPickupAtDepot(o, depot, depotAddress) ? o.Delivery : o.Pickup))
                    .ThenBy(o => matrix.TravelMinutes(o.Pickup, o.Delivery))
                    .First();
                remaining.Remove(next);
                route.Add(next);
                current = next.Delivery;
            }

            if (route.Count > 0)
                routes.Add(route);
        }

        return routes;
    }

    public static bool IsPickupAtDepot(OrderForPlanning order, GeoPoint depot, string depotAddress) =>
        DepotPickupMatcher.IsPickupAtDepot(order.PickupAddress, order.Pickup, depot, depotAddress);

    public static IReadOnlyList<PlanningStop> BuildPlanningStops(
        IReadOnlyList<OrderForPlanning> orders,
        GeoPoint depot,
        string depotAddress)
    {
        var stops = new List<PlanningStop>();
        foreach (var order in orders)
        {
            if (!IsPickupAtDepot(order, depot, depotAddress))
            {
                stops.Add(new PlanningStop(
                    order.Id,
                    "Pickup",
                    order.PickupAddress,
                    order.Pickup,
                    order.RecipientName,
                    order.ParcelDescription));
            }

            stops.Add(new PlanningStop(
                order.Id,
                "Dropoff",
                order.DeliveryAddress,
                order.Delivery,
                order.RecipientName,
                order.ParcelDescription));
        }

        return stops;
    }

    public static HashSet<Guid> PickedUpAtDepotOrderIds(
        IReadOnlyList<OrderForPlanning> orders,
        GeoPoint depot,
        string depotAddress) =>
        orders.Where(o => IsPickupAtDepot(o, depot, depotAddress)).Select(o => o.Id).ToHashSet();

    public static IReadOnlyList<PlanningStop> BuildGreedyVehicleRoute(
        GeoPoint depot,
        string depotAddress,
        IReadOnlyList<OrderForPlanning> orders,
        int maxOrdersPerRoute,
        ITravelTimeMatrix matrix)
    {
        var partition = PartitionOrdersAcrossVehicles(depot, depotAddress, orders, 1, maxOrdersPerRoute, matrix);
        var selected = partition.FirstOrDefault() ?? [];
        if (selected.Count == 0)
            return [];

        var stops = BuildPlanningStops(selected, depot, depotAddress);

        return OptimizeStopSequence(
            depot,
            stops,
            matrix,
            PickedUpAtDepotOrderIds(selected, depot, depotAddress));
    }

    public static IReadOnlyList<PlanningStop> OptimizeStopSequence(
        GeoPoint depot,
        IReadOnlyList<PlanningStop> stops,
        ITravelTimeMatrix matrix,
        IReadOnlySet<Guid>? pickedUpAtDepot = null)
    {
        var pickedUp = pickedUpAtDepot is null ? [] : pickedUpAtDepot.ToHashSet();
        var remaining = stops.ToList();
        var ordered = new List<PlanningStop>();
        var current = depot;

        while (remaining.Count > 0)
        {
            var eligible = remaining
                .Where(s => s.StopType == "Pickup"
                    || (s.OrderId.HasValue && pickedUp.Contains(s.OrderId.Value)))
                .ToList();

            if (eligible.Count == 0)
                throw new InvalidOperationException("Unable to build a valid stop sequence: dropoff before pickup.");

            var next = eligible
                .OrderBy(s => matrix.TravelMinutes(current, s.Location))
                .ThenBy(s => s.StopType == "Pickup" ? 0 : 1)
                .First();

            remaining.Remove(next);
            ordered.Add(next);
            if (next.StopType == "Pickup" && next.OrderId.HasValue)
                pickedUp.Add(next.OrderId.Value);
            current = next.Location;
        }

        return ordered;
    }

    public static int EstimateRouteMinutes(
        GeoPoint depot,
        IReadOnlyList<PlanningStop> orderedStops,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (orderedStops.Count == 0)
            return 0;

        var total = 0;
        var current = depot;
        foreach (var stop in orderedStops)
        {
            total += matrix.TravelMinutes(current, stop.Location);
            total += serviceMinutesPerStop;
            current = stop.Location;
        }

        total += matrix.TravelMinutes(current, depot);
        return total;
    }
}
