namespace UniConnect.Delivery;



public static class FixedRouteScheduleHelper

{

    public static DateOnly ComputeNextRouteDate(IEnumerable<DayOfWeek> routeDays, DateOnly fromDate)

    {

        var days = routeDays.Distinct().ToList();

        if (days.Count == 0)

            return fromDate;



        var offset = days.Min(d => DaysUntil(d, fromDate.DayOfWeek));

        return fromDate.AddDays(offset);

    }



    public static int DaysUntil(DayOfWeek routeDay, DayOfWeek fromDay) =>

        ((int)routeDay - (int)fromDay + 7) % 7;



    public static bool IncludesDayOfWeek(IEnumerable<DayOfWeek> routeDays, DayOfWeek day) =>

        routeDays.Contains(day);



    public static IReadOnlyList<DayOfWeek> NormalizeRouteDays(IEnumerable<DayOfWeek> routeDays) =>

        routeDays.Distinct().OrderBy(d => (int)d).ToList();



    public static void ValidateRouteDays(IReadOnlyList<DayOfWeek> routeDays)

    {

        if (routeDays is null || routeDays.Count == 0)

            throw new ArgumentException("Select at least one route day.");



        if (routeDays.Distinct().Count() != routeDays.Count)

            throw new ArgumentException("Duplicate route days are not allowed.");

    }



    public static string FormatDayOfWeek(DayOfWeek day) => day switch

    {

        DayOfWeek.Monday => "Monday",

        DayOfWeek.Tuesday => "Tuesday",

        DayOfWeek.Wednesday => "Wednesday",

        DayOfWeek.Thursday => "Thursday",

        DayOfWeek.Friday => "Friday",

        DayOfWeek.Saturday => "Saturday",

        DayOfWeek.Sunday => "Sunday",

        _ => day.ToString()

    };



    public static string FormatDaysOfWeek(IEnumerable<DayOfWeek> days)

    {

        var names = NormalizeRouteDays(days).Select(FormatDayOfWeek).ToList();

        if (names.Count == 0)

            return "—";

        if (names.Count == 1)

            return $"{names[0]}s";

        if (names.Count == 2)

            return $"{names[0]}s and {names[1]}s";



        return string.Join(", ", names.Take(names.Count - 1)) + ", and " + names[^1] + "s";

    }

}

