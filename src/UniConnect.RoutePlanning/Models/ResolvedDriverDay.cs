namespace UniConnect.RoutePlanning.Models;

/// <summary>Driver availability resolved on read for a specific calendar date.</summary>
public sealed class ResolvedDriverDay
{
    public Guid DriverId { get; init; }
    public DateOnly Date { get; init; }
    public bool IsWorking { get; init; }
    public TimeOnly ShiftStart { get; init; }
    public TimeOnly ShiftEnd { get; init; }
    public int LunchMinutes { get; init; }
    public int BreakMinutes { get; init; }
    public int? MaxRouteMinutes { get; init; }
    public TimeOnly? ReturnByTime { get; init; }

    public TimeOnly? OffBlockStart { get; init; }

    public TimeOnly? OffBlockEnd { get; init; }

    /// <summary>pattern, exception, or profile</summary>
    public string Source { get; init; } = "pattern";

    public Guid? ScheduleExceptionId { get; init; }

    public string? ScheduleNote { get; init; }

    public int AvailableWorkMinutes =>
        IsWorking
            ? DriverSchedule.AvailableWorkMinutes(
                ShiftStart, ShiftEnd, LunchMinutes, BreakMinutes, OffBlockStart, OffBlockEnd)
            : 0;

    public IReadOnlyList<(TimeOnly Start, TimeOnly End)> BlockedIntervals =>
        IsWorking
            ? DriverSchedule.BuildBlockedIntervals(
                ShiftStart, ShiftEnd, LunchMinutes, OffBlockStart, OffBlockEnd)
            : [];
}
