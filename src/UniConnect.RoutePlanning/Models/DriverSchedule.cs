namespace UniConnect.RoutePlanning.Models;

public static class DriverSchedule
{
    public static readonly TimeOnly DefaultShiftStart = new(7, 0);
    public static readonly TimeOnly DefaultShiftEnd = new(17, 0);
    public const int DefaultLunchMinutes = 30;
    public const int DefaultBreakMinutes = 15;

    public static TimeOnly NormalizeShiftStart(TimeOnly shiftStart) =>
        shiftStart == default ? DefaultShiftStart : shiftStart;

    public static TimeOnly NormalizeShiftEnd(TimeOnly shiftEnd) =>
        shiftEnd == default ? DefaultShiftEnd : shiftEnd;

    public static int ShiftLengthMinutes(TimeOnly shiftStart, TimeOnly shiftEnd)
    {
        shiftStart = NormalizeShiftStart(shiftStart);
        shiftEnd = NormalizeShiftEnd(shiftEnd);
        var minutes = (int)Math.Round((shiftEnd.ToTimeSpan() - shiftStart.ToTimeSpan()).TotalMinutes);
        if (minutes <= 0)
            minutes += 24 * 60;
        return minutes;
    }

    public static int AvailableWorkMinutes(
        TimeOnly shiftStart,
        TimeOnly shiftEnd,
        int lunchMinutes,
        int breakMinutes,
        TimeOnly? offBlockStart = null,
        TimeOnly? offBlockEnd = null)
    {
        shiftStart = NormalizeShiftStart(shiftStart);
        shiftEnd = NormalizeShiftEnd(shiftEnd);
        var shiftMinutes = ShiftLengthMinutes(shiftStart, shiftEnd);
        var blocked = MergedBlockedMinutes(
            BuildBlockedIntervals(shiftStart, shiftEnd, lunchMinutes, offBlockStart, offBlockEnd));
        return Math.Max(0, shiftMinutes - breakMinutes - blocked);
    }

    public static string FormatShiftWindow(TimeOnly start, TimeOnly end) =>
        $"{start:HH\\:mm}–{end:HH\\:mm}";

    public static string FormatWorkMinutes(int minutes)
    {
        if (minutes <= 0) return "0 min";
        if (minutes < 60) return $"{minutes} min";
        var hours = minutes / 60;
        var mins = minutes % 60;
        if (mins == 0) return $"{hours} hr";
        return $"{hours} hr {mins} min";
    }

    public static TimeOnly AddMinutes(TimeOnly time, int minutes) =>
        TimeOnly.FromTimeSpan(time.ToTimeSpan().Add(TimeSpan.FromMinutes(minutes)));

    /// <summary>Lunch block centered within the driver's shift window.</summary>
    public static (TimeOnly Start, TimeOnly End)? ComputeLunchBlock(
        TimeOnly shiftStart,
        TimeOnly shiftEnd,
        int lunchMinutes)
    {
        if (lunchMinutes <= 0)
            return null;

        shiftStart = NormalizeShiftStart(shiftStart);
        shiftEnd = NormalizeShiftEnd(shiftEnd);
        var shiftMinutes = ShiftLengthMinutes(shiftStart, shiftEnd);
        if (shiftMinutes <= lunchMinutes)
            return null;

        var beforeLunch = (shiftMinutes - lunchMinutes) / 2;
        var lunchStart = AddMinutes(shiftStart, beforeLunch);
        var lunchEnd = AddMinutes(lunchStart, lunchMinutes);
        return (lunchStart, lunchEnd);
    }

    public static (TimeOnly Start, TimeOnly End)? NormalizeOffBlock(
        TimeOnly shiftStart,
        TimeOnly shiftEnd,
        TimeOnly? offBlockStart,
        TimeOnly? offBlockEnd)
    {
        if (!offBlockStart.HasValue || !offBlockEnd.HasValue || offBlockEnd <= offBlockStart)
            return null;

        shiftStart = NormalizeShiftStart(shiftStart);
        shiftEnd = NormalizeShiftEnd(shiftEnd);
        var start = offBlockStart.Value < shiftStart ? shiftStart : offBlockStart.Value;
        var end = offBlockEnd.Value > shiftEnd ? shiftEnd : offBlockEnd.Value;
        return end > start ? (start, end) : null;
    }

    public static IReadOnlyList<(TimeOnly Start, TimeOnly End)> BuildBlockedIntervals(
        TimeOnly shiftStart,
        TimeOnly shiftEnd,
        int lunchMinutes,
        TimeOnly? offBlockStart = null,
        TimeOnly? offBlockEnd = null)
    {
        shiftStart = NormalizeShiftStart(shiftStart);
        shiftEnd = NormalizeShiftEnd(shiftEnd);
        var blocks = new List<(TimeOnly Start, TimeOnly End)>();

        var lunch = ComputeLunchBlock(shiftStart, shiftEnd, lunchMinutes);
        if (lunch.HasValue)
            blocks.Add(lunch.Value);

        var off = NormalizeOffBlock(shiftStart, shiftEnd, offBlockStart, offBlockEnd);
        if (off.HasValue)
            blocks.Add(off.Value);

        return blocks.OrderBy(b => b.Start).ToList();
    }

    public static int MergedBlockedMinutes(IReadOnlyList<(TimeOnly Start, TimeOnly End)> blocks)
    {
        if (blocks.Count == 0)
            return 0;

        var merged = new List<(TimeOnly Start, TimeOnly End)>();
        foreach (var block in blocks.OrderBy(b => b.Start))
        {
            if (merged.Count == 0 || block.Start > merged[^1].End)
                merged.Add(block);
            else if (block.End > merged[^1].End)
                merged[^1] = (merged[^1].Start, block.End);
        }

        return merged.Sum(b => (int)Math.Round((b.End - b.Start).TotalMinutes));
    }
}
