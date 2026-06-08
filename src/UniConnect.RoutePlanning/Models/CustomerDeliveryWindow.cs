namespace UniConnect.RoutePlanning.Models;

public sealed record CustomerDeliveryWindow(
    TimeOnly? OpenStart,
    TimeOnly? OpenEnd,
    TimeOnly? NoDeliveryStart,
    TimeOnly? NoDeliveryEnd)
{
    public const int InfeasiblePlacementMinutes = 2_000;
    public const int NoDeliveryOverlapMinutes = 1_500;

    public bool HasConstraints =>
        OpenStart.HasValue
        || OpenEnd.HasValue
        || (NoDeliveryStart.HasValue && NoDeliveryEnd.HasValue);

    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (OpenStart.HasValue || OpenEnd.HasValue)
                parts.Add($"open {Format(OpenStart)}–{Format(OpenEnd)}");
            if (NoDeliveryStart.HasValue && NoDeliveryEnd.HasValue)
                parts.Add($"no delivery {Format(NoDeliveryStart)}–{Format(NoDeliveryEnd)}");
            return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
        }
    }

    public static string? Format(TimeOnly? time) => time?.ToString("HH\\:mm");

    public WindowArrivalEvaluation EvaluateArrival(TimeOnly rawArrival, int serviceMinutes)
    {
        var waitMinutes = 0;
        var arrival = rawArrival;

        if (OpenStart.HasValue && arrival < OpenStart.Value)
        {
            waitMinutes += MinutesBetween(arrival, OpenStart.Value);
            arrival = OpenStart.Value;
        }

        if (NoDeliveryStart.HasValue && NoDeliveryEnd.HasValue
            && IsTimeInRange(arrival, NoDeliveryStart.Value, NoDeliveryEnd.Value))
        {
            waitMinutes += MinutesBetween(arrival, NoDeliveryEnd.Value);
            arrival = NoDeliveryEnd.Value;
        }

        var isAfterClose = OpenEnd.HasValue && arrival > OpenEnd.Value;
        var departure = AddMinutes(arrival, serviceMinutes);
        var overlapsNoDelivery = NoDeliveryStart.HasValue && NoDeliveryEnd.HasValue
            && OverlapsInterval(arrival, departure, NoDeliveryStart.Value, NoDeliveryEnd.Value);

        return new WindowArrivalEvaluation(rawArrival, arrival, departure, waitMinutes, isAfterClose, overlapsNoDelivery);
    }

    public int PlacementScoreMinutes(TimeOnly rawArrival, int serviceMinutes)
    {
        var evaluation = EvaluateArrival(rawArrival, serviceMinutes);
        if (evaluation.IsAfterClose)
            return InfeasiblePlacementMinutes + MinutesBetween(OpenEnd!.Value, evaluation.AdjustedArrival);

        if (evaluation.OverlapsNoDeliveryBlock)
            return NoDeliveryOverlapMinutes;

        return evaluation.WaitMinutes;
    }

    public TimeOnly? EffectiveLatestArrival(int serviceMinutes)
    {
        if (!HasConstraints)
            return null;

        var candidates = new List<TimeOnly>();
        if (OpenEnd.HasValue)
            candidates.Add(AddMinutes(OpenEnd.Value, -serviceMinutes));

        if (NoDeliveryStart.HasValue)
            candidates.Add(AddMinutes(NoDeliveryStart.Value, -serviceMinutes));

        if (candidates.Count == 0)
            return null;

        var latest = candidates.Min();
        if (OpenStart.HasValue && latest < OpenStart.Value)
            return OpenStart.Value;

        return latest;
    }

    public static TimeOnly AddMinutes(TimeOnly time, int minutes)
    {
        var total = time.ToTimeSpan().Add(TimeSpan.FromMinutes(minutes));
        if (total.TotalMinutes >= 24 * 60)
            total = total.Subtract(TimeSpan.FromDays(1));
        return TimeOnly.FromTimeSpan(total);
    }

    private static int MinutesBetween(TimeOnly from, TimeOnly to)
    {
        var diff = to.ToTimeSpan() - from.ToTimeSpan();
        return (int)Math.Max(0, diff.TotalMinutes);
    }

    private static bool IsTimeInRange(TimeOnly time, TimeOnly start, TimeOnly end)
    {
        if (start <= end)
            return time >= start && time < end;

        return time >= start || time < end;
    }

    private static bool OverlapsInterval(TimeOnly start, TimeOnly end, TimeOnly blockStart, TimeOnly blockEnd)
    {
        var startMin = ToMinutes(start);
        var endMin = ToMinutes(end);
        var blockStartMin = ToMinutes(blockStart);
        var blockEndMin = ToMinutes(blockEnd);

        if (blockStartMin <= blockEndMin)
            return startMin < blockEndMin && endMin > blockStartMin;

        return startMin < blockEndMin || endMin > blockStartMin;
    }

    private static int ToMinutes(TimeOnly time) => time.Hour * 60 + time.Minute;
}

public sealed record WindowArrivalEvaluation(
    TimeOnly RawArrival,
    TimeOnly AdjustedArrival,
    TimeOnly Departure,
    int WaitMinutes,
    bool IsAfterClose,
    bool OverlapsNoDeliveryBlock);
