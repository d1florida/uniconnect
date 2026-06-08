namespace UniConnect.Insights;

public static class DriverScheduleHelper
{
    public static readonly TimeOnly DefaultShiftStart = new(7, 0);
    public static readonly TimeOnly DefaultShiftEnd = new(17, 0);
    public const int DefaultLunchMinutes = 30;
    public const int DefaultBreakMinutes = 15;

    public static int AvailableWorkMinutes(TimeOnly shiftStart, TimeOnly shiftEnd, int lunchMinutes, int breakMinutes)
    {
        var shift = shiftEnd.ToTimeSpan() - shiftStart.ToTimeSpan();
        var minutes = (int)Math.Round(shift.TotalMinutes);
        if (minutes <= 0)
            minutes += 24 * 60;
        return Math.Max(0, minutes - lunchMinutes - breakMinutes);
    }

    public static TimeOnly ParseShiftTime(string? value, TimeOnly fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        return TimeOnly.TryParse(value, out var parsed) ? parsed : fallback;
    }

    public static string FormatShiftTime(TimeOnly time) => time.ToString("HH:mm");
}
