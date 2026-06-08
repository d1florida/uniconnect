using Microsoft.EntityFrameworkCore;
using UniConnect.Infrastructure.Data;
using UniConnect.Insights.Entities;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;

namespace UniConnect.Infrastructure.Services.Insights;

public sealed class DriverScheduleResolver(AppDbContext db) : IDriverScheduleResolver
{
    public async Task<IReadOnlyDictionary<Guid, ResolvedDriverDay>> ResolveAsync(
        Guid tenantId,
        DateOnly date,
        IReadOnlyList<Guid> driverIds,
        CancellationToken ct = default)
    {
        var range = await ResolveRangeAsync(tenantId, date, date, driverIds, ct);
        return range.ToDictionary(kv => kv.Key, kv => kv.Value[date]);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateOnly, ResolvedDriverDay>>> ResolveRangeAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Guid> driverIds,
        CancellationToken ct = default)
    {
        if (driverIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyDictionary<DateOnly, ResolvedDriverDay>>();

        var drivers = await db.Drivers.AsNoTracking()
            .Where(d => d.TenantId == tenantId && driverIds.Contains(d.Id))
            .ToListAsync(ct);

        var patterns = await db.DriverWorkPatterns.AsNoTracking()
            .Where(p => driverIds.Contains(p.DriverId))
            .ToListAsync(ct);

        var exceptions = await db.DriverScheduleExceptions.AsNoTracking()
            .Where(e => driverIds.Contains(e.DriverId) && e.Date >= from && e.Date <= to)
            .ToListAsync(ct);

        var patternsByDriver = patterns
            .GroupBy(p => p.DriverId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(p => p.DayOfWeek));

        var exceptionsByDriver = exceptions
            .GroupBy(e => e.DriverId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.Date));

        var dates = EnumerateDates(from, to).ToList();
        var result = new Dictionary<Guid, IReadOnlyDictionary<DateOnly, ResolvedDriverDay>>(drivers.Count);

        foreach (var driver in drivers)
        {
            patternsByDriver.TryGetValue(driver.Id, out var byDay);
            exceptionsByDriver.TryGetValue(driver.Id, out var byDate);

            var days = new Dictionary<DateOnly, ResolvedDriverDay>(dates.Count);
            foreach (var date in dates)
            {
                DriverWorkPattern? pattern = null;
                if (byDay is not null)
                    byDay.TryGetValue(date.DayOfWeek, out pattern);

                DriverScheduleException? exception = null;
                if (byDate is not null)
                    byDate.TryGetValue(date, out exception);

                days[date] = DriverScheduleResolution.Resolve(driver, date, pattern, exception);
            }

            result[driver.Id] = days;
        }

        return result;
    }

    private static IEnumerable<DateOnly> EnumerateDates(DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
            yield return date;
    }
}
