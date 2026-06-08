namespace UniConnect.Insights.Entities;

/// <summary>Date-specific override to a driver's weekly work pattern (PTO, swapped shift, etc.).</summary>
public class DriverScheduleException
{
    public Guid Id { get; set; }
    public Guid DriverId { get; set; }
    public DateOnly Date { get; set; }
    public bool IsWorking { get; set; }
    public TimeOnly ShiftStartTime { get; set; } = new(7, 0);
    public TimeOnly ShiftEndTime { get; set; } = new(17, 0);
    public int LunchMinutes { get; set; } = 30;
    public int BreakMinutes { get; set; } = 15;
    public int? MaxRouteMinutes { get; set; }
    public TimeOnly? ReturnByTime { get; set; }
    public TimeOnly? OffBlockStartTime { get; set; }
    public TimeOnly? OffBlockEndTime { get; set; }
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Driver Driver { get; set; } = null!;
}
