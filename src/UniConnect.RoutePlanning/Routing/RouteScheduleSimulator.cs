using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.RoutePlanning.Routing;

public enum DeliveryWindowViolationKind
{
    BeforeOpen,
    AfterClose,
    DuringNoDelivery
}

public sealed record DeliveryWindowViolation(
    DeliveryWindowViolationKind Kind,
    string Message);

public sealed record SimulatedStopSchedule(
    TimeOnly Arrival,
    TimeOnly Departure,
    int WaitMinutes,
    IReadOnlyList<DeliveryWindowViolation> Violations);

public static class RouteScheduleSimulator
{
    public const int ViolationPenaltyMinutes = 120;

    public static IReadOnlyList<SimulatedStopSchedule> Simulate(
        GeoPoint depot,
        IReadOnlyList<PlanningStop> stops,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop,
        TimeOnly routeStartTime,
        TimeOnly? shiftEnd = null,
        int lunchMinutes = 0,
        TimeOnly? offBlockStart = null,
        TimeOnly? offBlockEnd = null)
    {
        var blocked = DriverSchedule.BuildBlockedIntervals(
            routeStartTime,
            shiftEnd ?? DriverSchedule.DefaultShiftEnd,
            lunchMinutes,
            offBlockStart,
            offBlockEnd);
        return Simulate(depot, stops, matrix, serviceMinutesPerStop, routeStartTime, blocked);
    }

    public static IReadOnlyList<SimulatedStopSchedule> Simulate(
        GeoPoint depot,
        IReadOnlyList<PlanningStop> stops,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop,
        TimeOnly routeStartTime,
        IReadOnlyList<(TimeOnly Start, TimeOnly End)> blockedIntervals)
    {
        var blocks = blockedIntervals.OrderBy(b => b.Start).ToList();
        var results = new List<SimulatedStopSchedule>(stops.Count);
        var currentTime = routeStartTime;
        var currentLocation = depot;

        foreach (var stop in stops)
        {
            var travel = matrix.TravelMinutes(currentLocation, stop.Location);
            var rawArrival = CustomerDeliveryWindow.AddMinutes(currentTime, travel);
            var violations = new List<DeliveryWindowViolation>();
            var waitMinutes = 0;
            TimeOnly arrival;
            TimeOnly departure;

            if (stop.StopType == "Dropoff" && stop.DeliveryWindow is { HasConstraints: true } window)
            {
                var adjustedArrival = ApplyBlockedWait(rawArrival, blocks, ref waitMinutes);
                var evaluation = window.EvaluateArrival(adjustedArrival, serviceMinutesPerStop);
                arrival = evaluation.AdjustedArrival;
                departure = ComputeDepartureWithBlocks(arrival, serviceMinutesPerStop, blocks);
                waitMinutes += evaluation.WaitMinutes;

                if (window.OpenStart.HasValue && rawArrival < window.OpenStart.Value)
                {
                    violations.Add(new DeliveryWindowViolation(
                        DeliveryWindowViolationKind.BeforeOpen,
                        $"Arrival {FormatTime(rawArrival)} is before open {CustomerDeliveryWindow.Format(window.OpenStart)}"));
                }

                if (evaluation.IsAfterClose)
                {
                    violations.Add(new DeliveryWindowViolation(
                        DeliveryWindowViolationKind.AfterClose,
                        $"Arrival {FormatTime(arrival)} is after close {CustomerDeliveryWindow.Format(window.OpenEnd)}"));
                }

                if (evaluation.OverlapsNoDeliveryBlock)
                {
                    violations.Add(new DeliveryWindowViolation(
                        DeliveryWindowViolationKind.DuringNoDelivery,
                        $"Service overlaps no-delivery block {CustomerDeliveryWindow.Format(window.NoDeliveryStart)}–{CustomerDeliveryWindow.Format(window.NoDeliveryEnd)}"));
                }
            }
            else
            {
                arrival = ApplyBlockedWait(rawArrival, blocks, ref waitMinutes);
                departure = ComputeDepartureWithBlocks(arrival, serviceMinutesPerStop, blocks);
            }

            results.Add(new SimulatedStopSchedule(arrival, departure, waitMinutes, violations));
            currentTime = departure;
            currentLocation = stop.Location;
        }

        return results;
    }

    public static int CountViolations(IReadOnlyList<SimulatedStopSchedule> schedule) =>
        schedule.Sum(s => s.Violations.Count);

    public static int CompositeRouteCostMinutes(
        GeoPoint depot,
        IReadOnlyList<PlanningStop> stops,
        ITravelTimeMatrix matrix,
        int serviceMinutesPerStop,
        TimeOnly routeStartTime,
        TimeOnly? shiftEnd = null,
        int lunchMinutes = 0,
        TimeOnly? offBlockStart = null,
        TimeOnly? offBlockEnd = null)
    {
        var travel = PickupDeliveryRouteOptimizer.EstimateRouteMinutes(depot, stops, matrix, serviceMinutesPerStop);
        var blocks = DriverSchedule.BuildBlockedIntervals(
            routeStartTime,
            shiftEnd ?? DriverSchedule.DefaultShiftEnd,
            lunchMinutes,
            offBlockStart,
            offBlockEnd);
        if (!stops.Any(s => s.DeliveryWindow is { HasConstraints: true }) && blocks.Count == 0)
            return travel;

        var schedule = Simulate(depot, stops, matrix, serviceMinutesPerStop, routeStartTime, blocks);
        var windowCost = schedule.Sum(s =>
            s.Violations.Count * ViolationPenaltyMinutes + s.WaitMinutes);
        return travel + windowCost;
    }

    public static string FormatTime(TimeOnly time) => time.ToString("HH\\:mm");

    private static TimeOnly ApplyBlockedWait(
        TimeOnly arrival,
        IReadOnlyList<(TimeOnly Start, TimeOnly End)> blocks,
        ref int waitMinutes)
    {
        var cursor = arrival;
        foreach (var block in blocks)
        {
            if (cursor >= block.Start && cursor < block.End)
            {
                waitMinutes += (int)Math.Round((block.End - cursor).TotalMinutes);
                cursor = block.End;
            }
        }

        return cursor;
    }

    private static TimeOnly ComputeDepartureWithBlocks(
        TimeOnly arrival,
        int serviceMinutes,
        IReadOnlyList<(TimeOnly Start, TimeOnly End)> blocks)
    {
        if (serviceMinutes <= 0)
            return arrival;
        if (blocks.Count == 0)
            return CustomerDeliveryWindow.AddMinutes(arrival, serviceMinutes);

        var cursor = arrival;
        var remaining = serviceMinutes;

        foreach (var block in blocks)
        {
            if (cursor >= block.Start && cursor < block.End)
                cursor = block.End;

            if (cursor < block.Start)
            {
                var untilBlock = (int)Math.Round((block.Start - cursor).TotalMinutes);
                if (remaining <= untilBlock)
                    return CustomerDeliveryWindow.AddMinutes(cursor, remaining);

                remaining -= untilBlock;
                cursor = block.End;
            }
        }

        return CustomerDeliveryWindow.AddMinutes(cursor, remaining);
    }
}
