using UniConnect.Insights;
using UniConnect.Insights.Entities;

namespace UniConnect.Infrastructure.Services.Insights;

internal static class DriverWorkPatternHelper
{
    public static readonly DayOfWeek[] AllDays =
    [
        DayOfWeek.Sunday,
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday
    ];

    public static bool IsWeekday(DayOfWeek day) =>
        day is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

    public static IReadOnlyList<DriverWorkPattern> CreateDefaultPatterns(Driver driver)
    {
        return AllDays.Select(day => new DriverWorkPattern
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            DayOfWeek = day,
            IsWorkingDay = IsWeekday(day),
            ShiftStartTime = driver.ShiftStartTime,
            ShiftEndTime = driver.ShiftEndTime,
            LunchMinutes = driver.LunchMinutes,
            BreakMinutes = driver.BreakMinutes,
            MaxRouteMinutes = driver.MaxRouteMinutes,
            ReturnByTime = driver.ReturnByTime
        }).ToList();
    }

    public static void SyncWeekdayPatternsFromProfile(Driver driver, IEnumerable<DriverWorkPattern> patterns)
    {
        foreach (var pattern in patterns.Where(p => IsWeekday(p.DayOfWeek)))
        {
            pattern.ShiftStartTime = driver.ShiftStartTime;
            pattern.ShiftEndTime = driver.ShiftEndTime;
            pattern.LunchMinutes = driver.LunchMinutes;
            pattern.BreakMinutes = driver.BreakMinutes;
            pattern.MaxRouteMinutes = driver.MaxRouteMinutes;
            pattern.ReturnByTime = driver.ReturnByTime;
        }
    }
}
