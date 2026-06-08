using UniConnect.RoutePlanning.Models;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public static class PlanningPolicyResolver
{
    public static int ResolveEffectiveRouteCapMinutes(ResolvedDriverDay day)
    {
        if (!day.IsWorking)
            return 0;

        var cap = day.AvailableWorkMinutes;

        if (day.MaxRouteMinutes is int max && max > 0)
            cap = Math.Min(cap, max);

        if (day.ReturnByTime is TimeOnly returnBy)
            cap = Math.Min(cap, MinutesUntilReturn(day.ShiftStart, day.LunchMinutes, day.BreakMinutes, returnBy));

        return Math.Max(1, cap);
    }

    private static int MinutesUntilReturn(TimeOnly shiftStart, int lunchMinutes, int breakMinutes, TimeOnly returnBy)
    {
        var start = DriverSchedule.NormalizeShiftStart(shiftStart);
        var returnMinutes = returnBy.ToTimeSpan().TotalMinutes - start.ToTimeSpan().TotalMinutes;
        returnMinutes -= lunchMinutes + breakMinutes;
        return returnMinutes > 0 ? (int)returnMinutes : int.MaxValue;
    }
}
