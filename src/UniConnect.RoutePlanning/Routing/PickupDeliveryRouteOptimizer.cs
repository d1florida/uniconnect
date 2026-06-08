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
    Guid? RouteStopId = null,
    CustomerDeliveryWindow? DeliveryWindow = null);

public sealed record OrderForPlanning(
    Guid Id,
    string PickupAddress,
    string DeliveryAddress,
    GeoPoint Pickup,
    GeoPoint Delivery,
    string RecipientName,
    string ParcelDescription,
    CustomerDeliveryWindow? DeliveryWindow = null);

public static class PickupDeliveryRouteOptimizer
{
    public static IReadOnlyList<IReadOnlyList<OrderForPlanning>> PartitionOrdersAcrossVehicles(
        GeoPoint depot,
        string depotAddress,
        IReadOnlyList<OrderForPlanning> orders,
        int vehicleCount,
        int maxOrdersPerRoute,
        ITravelTimeMatrix matrix,
        int? maxRouteMinutes = null,
        int serviceMinutesPerStop = 5) =>
        ClarkeWrightPartitioner.Partition(
            depot,
            depotAddress,
            orders,
            vehicleCount,
            maxOrdersPerRoute,
            matrix,
            maxRouteMinutes,
            serviceMinutesPerStop);

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
                order.ParcelDescription,
                DeliveryWindow: order.DeliveryWindow));
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
        IReadOnlySet<Guid>? pickedUpAtDepot = null,
        TimeOnly? routeStartTime = null,
        int serviceMinutesPerStop = 5)
    {
        var pickedUp = pickedUpAtDepot is null ? [] : pickedUpAtDepot.ToHashSet();
        var startTime = routeStartTime ?? DriverSchedule.DefaultShiftStart;
        var greedy = OptimizeStopSequenceGreedy(depot, stops, matrix, pickedUp, startTime, serviceMinutesPerStop);
        return ImproveStopSequenceTwoOpt(depot, greedy, matrix, pickedUp, startTime, serviceMinutesPerStop);
    }

    private static List<PlanningStop> OptimizeStopSequenceGreedy(
        GeoPoint depot,
        IReadOnlyList<PlanningStop> stops,
        ITravelTimeMatrix matrix,
        HashSet<Guid> pickedUp,
        TimeOnly routeStartTime,
        int serviceMinutesPerStop)
    {
        var remaining = stops.ToList();
        var ordered = new List<PlanningStop>();
        var current = depot;
        var currentTime = routeStartTime;
        var hasWindows = stops.Any(s => s.DeliveryWindow is { HasConstraints: true });

        while (remaining.Count > 0)
        {
            var eligible = remaining
                .Where(s => s.StopType == "Pickup"
                    || (s.OrderId.HasValue && pickedUp.Contains(s.OrderId.Value)))
                .ToList();

            if (eligible.Count == 0)
                throw new InvalidOperationException("Unable to build a valid stop sequence: dropoff before pickup.");

            var next = hasWindows
                ? SelectNextStopWithWindows(eligible, current, currentTime, matrix, serviceMinutesPerStop)
                : eligible
                    .OrderBy(s => matrix.TravelMinutes(current, s.Location))
                    .ThenBy(s => s.StopType == "Pickup" ? 0 : 1)
                    .First();

            remaining.Remove(next);
            ordered.Add(next);
            if (next.StopType == "Pickup" && next.OrderId.HasValue)
                pickedUp.Add(next.OrderId.Value);

            var travel = matrix.TravelMinutes(current, next.Location);
            var arrival = CustomerDeliveryWindow.AddMinutes(currentTime, travel);
            if (next.StopType == "Dropoff" && next.DeliveryWindow is { HasConstraints: true } window)
                arrival = window.EvaluateArrival(arrival, serviceMinutesPerStop).AdjustedArrival;

            currentTime = CustomerDeliveryWindow.AddMinutes(arrival, serviceMinutesPerStop);
            current = next.Location;
        }

        return ordered;
    }

    private static PlanningStop SelectNextStopWithWindows(
        IReadOnlyList<PlanningStop> eligible,
        GeoPoint current,
        TimeOnly currentTime,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        var urgent = eligible
            .Select(s => new
            {
                Stop = s,
                CriticalSlackMinutes = ComputeCriticalSlackMinutes(s, current, currentTime, matrix, serviceMinutesPerStop)
            })
            .Where(x => x.CriticalSlackMinutes.HasValue)
            .OrderBy(x => x.CriticalSlackMinutes!.Value)
            .FirstOrDefault();

        if (urgent?.CriticalSlackMinutes <= 120)
            return urgent.Stop;

        return eligible
            .OrderBy(s => ScoreStopCandidate(s, current, currentTime, matrix, serviceMinutesPerStop))
            .ThenBy(s => s.StopType == "Pickup" ? 0 : 1)
            .First();
    }

    private static int? ComputeCriticalSlackMinutes(
        PlanningStop stop,
        GeoPoint current,
        TimeOnly currentTime,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (stop.StopType != "Dropoff" || stop.DeliveryWindow is not { HasConstraints: true } window)
            return null;

        var deadline = window.EffectiveLatestArrival(serviceMinutesPerStop);
        if (!deadline.HasValue)
            return null;

        var travel = matrix.TravelMinutes(current, stop.Location);
        var mustArriveBy = deadline.Value;
        var rawArrival = CustomerDeliveryWindow.AddMinutes(currentTime, travel);
        return (int)(mustArriveBy.ToTimeSpan() - rawArrival.ToTimeSpan()).TotalMinutes;
    }

    private static int ScoreStopCandidate(
        PlanningStop stop,
        GeoPoint current,
        TimeOnly currentTime,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        var travel = matrix.TravelMinutes(current, stop.Location);
        var rawArrival = CustomerDeliveryWindow.AddMinutes(currentTime, travel);
        if (stop.StopType != "Dropoff" || stop.DeliveryWindow is not { HasConstraints: true } window)
            return travel;

        var windowScore = window.PlacementScoreMinutes(rawArrival, serviceMinutesPerStop);
        var deadline = window.EffectiveLatestArrival(serviceMinutesPerStop);
        if (deadline.HasValue && rawArrival > deadline.Value)
        {
            windowScore += CustomerDeliveryWindow.InfeasiblePlacementMinutes
                + (int)(rawArrival.ToTimeSpan() - deadline.Value.ToTimeSpan()).TotalMinutes;
        }

        return travel + windowScore;
    }

    private static IReadOnlyList<PlanningStop> ImproveStopSequenceTwoOpt(
        GeoPoint depot,
        IReadOnlyList<PlanningStop> sequence,
        ITravelTimeMatrix matrix,
        HashSet<Guid> pickedUpAtDepot,
        TimeOnly routeStartTime,
        int serviceMinutesPerStop)
    {
        if (sequence.Count < 3)
            return sequence;

        var hasWindows = sequence.Any(s => s.DeliveryWindow is { HasConstraints: true });
        var route = sequence.ToList();
        var improved = true;

        while (improved)
        {
            improved = false;
            var currentCost = hasWindows
                ? RouteScheduleSimulator.CompositeRouteCostMinutes(depot, route, matrix, serviceMinutesPerStop, routeStartTime)
                : RouteTravelMinutes(depot, route, matrix);

            for (var i = 0; i < route.Count - 1; i++)
            {
                for (var j = i + 1; j < route.Count; j++)
                {
                    var candidate = ReverseSegment(route, i, j);
                    if (!IsValidStopSequence(candidate, pickedUpAtDepot))
                        continue;

                    var candidateCost = hasWindows
                        ? RouteScheduleSimulator.CompositeRouteCostMinutes(depot, candidate, matrix, serviceMinutesPerStop, routeStartTime)
                        : RouteTravelMinutes(depot, candidate, matrix);
                    if (candidateCost >= currentCost)
                        continue;

                    route = candidate;
                    currentCost = candidateCost;
                    improved = true;
                }
            }
        }

        return route;
    }

    private static List<PlanningStop> ReverseSegment(IReadOnlyList<PlanningStop> route, int start, int end)
    {
        var copy = route.ToList();
        copy.Reverse(start, end - start + 1);
        return copy;
    }

    private static bool IsValidStopSequence(IReadOnlyList<PlanningStop> sequence, IReadOnlySet<Guid> pickedUpAtDepot)
    {
        var pickedUp = pickedUpAtDepot.ToHashSet();
        var pickupIndex = new Dictionary<Guid, int>();
        var dropoffIndex = new Dictionary<Guid, int>();

        for (var i = 0; i < sequence.Count; i++)
        {
            var stop = sequence[i];
            if (!stop.OrderId.HasValue)
                continue;

            if (stop.StopType == "Pickup")
                pickupIndex[stop.OrderId.Value] = i;
            else if (stop.StopType == "Dropoff")
                dropoffIndex[stop.OrderId.Value] = i;
        }

        foreach (var orderId in dropoffIndex.Keys)
        {
            if (pickedUpAtDepot.Contains(orderId))
                continue;

            if (!pickupIndex.TryGetValue(orderId, out var pickup) || dropoffIndex[orderId] <= pickup)
                return false;
        }

        return true;
    }

    private static int RouteTravelMinutes(GeoPoint depot, IReadOnlyList<PlanningStop> stops, ITravelTimeMatrix matrix)
    {
        if (stops.Count == 0)
            return 0;

        var total = 0;
        var current = depot;
        foreach (var stop in stops)
        {
            total += matrix.TravelMinutes(current, stop.Location);
            current = stop.Location;
        }

        total += matrix.TravelMinutes(current, depot);
        return total;
    }

    public static int EstimateRouteMinutes(
        GeoPoint depot,
        IReadOnlyList<PlanningStop> orderedStops,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop)
    {
        if (orderedStops.Count == 0)
            return 0;

        var total = RouteTravelMinutes(depot, orderedStops, matrix);
        total += serviceMinutesPerStop * orderedStops.Count;
        return total;
    }
}
