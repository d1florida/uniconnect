namespace UniConnect.Insights.Entities;

/// <summary>Recurring weekly availability for a driver (one row per day of week).</summary>
public class DriverWorkPattern
{
    public Guid Id { get; set; }
    public Guid DriverId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public TimeOnly ShiftStartTime { get; set; } = new(7, 0);
    public TimeOnly ShiftEndTime { get; set; } = new(17, 0);
    public int LunchMinutes { get; set; } = 30;
    public int BreakMinutes { get; set; } = 15;
    public int? MaxRouteMinutes { get; set; }
    public TimeOnly? ReturnByTime { get; set; }

    public Driver Driver { get; set; } = null!;
}
