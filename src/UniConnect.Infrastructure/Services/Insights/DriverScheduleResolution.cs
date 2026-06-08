using UniConnect.Insights.Entities;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.Infrastructure.Services.Insights;

internal static class DriverScheduleResolution
{
    public static ResolvedDriverDay FromException(Driver driver, DateOnly date, DriverScheduleException exception) =>
        exception.IsWorking
            ? new ResolvedDriverDay
            {
                DriverId = driver.Id,
                Date = date,
                IsWorking = true,
                ShiftStart = DriverSchedule.NormalizeShiftStart(exception.ShiftStartTime),
                ShiftEnd = DriverSchedule.NormalizeShiftEnd(exception.ShiftEndTime),
                LunchMinutes = exception.LunchMinutes,
                BreakMinutes = exception.BreakMinutes,
                MaxRouteMinutes = exception.MaxRouteMinutes ?? driver.MaxRouteMinutes,
                ReturnByTime = exception.ReturnByTime ?? driver.ReturnByTime,
                OffBlockStart = exception.OffBlockStartTime,
                OffBlockEnd = exception.OffBlockEndTime,
                Source = "exception",
                ScheduleExceptionId = exception.Id,
                ScheduleNote = exception.Note
            }
            : new ResolvedDriverDay
            {
                DriverId = driver.Id,
                Date = date,
                IsWorking = false,
                ShiftStart = exception.ShiftStartTime,
                ShiftEnd = exception.ShiftEndTime,
                LunchMinutes = exception.LunchMinutes,
                BreakMinutes = exception.BreakMinutes,
                MaxRouteMinutes = exception.MaxRouteMinutes,
                ReturnByTime = exception.ReturnByTime,
                Source = "exception",
                ScheduleExceptionId = exception.Id,
                ScheduleNote = exception.Note
            };

    public static ResolvedDriverDay FromPattern(Driver driver, DateOnly date, DriverWorkPattern pattern) =>
        !pattern.IsWorkingDay
            ? new ResolvedDriverDay
            {
                DriverId = driver.Id,
                Date = date,
                IsWorking = false,
                ShiftStart = pattern.ShiftStartTime,
                ShiftEnd = pattern.ShiftEndTime,
                LunchMinutes = pattern.LunchMinutes,
                BreakMinutes = pattern.BreakMinutes,
                MaxRouteMinutes = pattern.MaxRouteMinutes,
                ReturnByTime = pattern.ReturnByTime,
                Source = "pattern"
            }
            : new ResolvedDriverDay
            {
                DriverId = driver.Id,
                Date = date,
                IsWorking = true,
                ShiftStart = DriverSchedule.NormalizeShiftStart(pattern.ShiftStartTime),
                ShiftEnd = DriverSchedule.NormalizeShiftEnd(pattern.ShiftEndTime),
                LunchMinutes = pattern.LunchMinutes,
                BreakMinutes = pattern.BreakMinutes,
                MaxRouteMinutes = pattern.MaxRouteMinutes ?? driver.MaxRouteMinutes,
                ReturnByTime = pattern.ReturnByTime ?? driver.ReturnByTime,
                Source = "pattern"
            };

    public static ResolvedDriverDay FromProfile(Driver driver, DateOnly date, DayOfWeek dayOfWeek)
    {
        var isWorking = DriverWorkPatternHelper.IsWeekday(dayOfWeek);
        return new ResolvedDriverDay
        {
            DriverId = driver.Id,
            Date = date,
            IsWorking = isWorking,
            ShiftStart = DriverSchedule.NormalizeShiftStart(driver.ShiftStartTime),
            ShiftEnd = DriverSchedule.NormalizeShiftEnd(driver.ShiftEndTime),
            LunchMinutes = driver.LunchMinutes,
            BreakMinutes = driver.BreakMinutes,
            MaxRouteMinutes = driver.MaxRouteMinutes,
            ReturnByTime = driver.ReturnByTime,
            Source = "profile"
        };
    }

    public static ResolvedDriverDay Resolve(
        Driver driver,
        DateOnly date,
        DriverWorkPattern? pattern,
        DriverScheduleException? exception)
    {
        if (exception is not null)
            return FromException(driver, date, exception);

        if (pattern is not null)
            return FromPattern(driver, date, pattern);

        return FromProfile(driver, date, date.DayOfWeek);
    }
}
