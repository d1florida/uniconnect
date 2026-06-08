using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.Delivery.Enums;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Identity;
using UniConnect.Insights;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;
using UniConnect.Insights.Interfaces;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services.Insights;

public class DriverDirectory(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser,
    IDriverScheduleResolver scheduleResolver) : IDriverDirectory
{
    public async Task<IReadOnlyList<DriverDto>> GetDriversAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var query = db.Drivers.AsNoTracking().Where(d => d.TenantId == tenantId);
        if (currentUser.IsDriver && currentUser.DriverId.HasValue)
            query = query.Where(d => d.Id == currentUser.DriverId.Value);

        var drivers = await query.OrderBy(d => d.DisplayName).ToListAsync(ct);
        return await MapDriversAsync(drivers, ct);
    }

    public async Task<DriverDto?> GetDriverAsync(Guid tenantId, Guid driverId, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        currentUser.EnsureDriverSelf(driverId);
        var driver = await db.Drivers.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == tenantId, ct);
        return driver is null ? null : (await MapDriversAsync([driver], ct))[0];
    }

    public async Task<DriverDto> CreateDriverAsync(Guid tenantId, CreateDriverRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.");

        await ValidateLinkedUserAsync(tenantId, request.UserId, excludeDriverId: null, ct);

        var schedule = ParseSchedule(request.ShiftStartTime, request.ShiftEndTime, request.LunchMinutes, request.BreakMinutes);
        var routeCap = ParseMaxRouteMinutes(request.MaxRouteMinutes);
        var returnBy = ParseReturnByTime(request.ReturnByTime);

        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DisplayName = displayName,
            UserId = request.UserId,
            IsActive = true,
            ShiftStartTime = schedule.ShiftStart,
            ShiftEndTime = schedule.ShiftEnd,
            LunchMinutes = schedule.LunchMinutes,
            BreakMinutes = schedule.BreakMinutes,
            MaxRouteMinutes = routeCap,
            ReturnByTime = returnBy,
            CreatedAt = DateTime.UtcNow
        };

        db.Drivers.Add(driver);
        db.DriverWorkPatterns.AddRange(DriverWorkPatternHelper.CreateDefaultPatterns(driver));
        await db.SaveChangesAsync(ct);
        if (request.UserId.HasValue)
            await SyncLinkedUserRoleAsync(tenantId, request.UserId.Value, ct);
        return (await GetDriverAsync(tenantId, driver.Id, ct))!;
    }

    public async Task<DriverDto> UpdateDriverAsync(Guid tenantId, Guid driverId, UpdateDriverRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Driver not found.");

        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.");

        var previousUserId = driver.UserId;
        await ValidateLinkedUserAsync(tenantId, request.UserId, excludeDriverId: driverId, ct);

        var schedule = ParseSchedule(
            request.ShiftStartTime ?? DriverScheduleHelper.FormatShiftTime(driver.ShiftStartTime),
            request.ShiftEndTime ?? DriverScheduleHelper.FormatShiftTime(driver.ShiftEndTime),
            request.LunchMinutes ?? driver.LunchMinutes,
            request.BreakMinutes ?? driver.BreakMinutes);

        driver.DisplayName = displayName;
        driver.UserId = request.UserId;
        driver.IsActive = request.IsActive;
        driver.ShiftStartTime = schedule.ShiftStart;
        driver.ShiftEndTime = schedule.ShiftEnd;
        driver.LunchMinutes = schedule.LunchMinutes;
        driver.BreakMinutes = schedule.BreakMinutes;
        driver.MaxRouteMinutes = request.MaxRouteMinutes.HasValue
            ? ParseMaxRouteMinutes(request.MaxRouteMinutes)
            : null;
        driver.ReturnByTime = string.IsNullOrWhiteSpace(request.ReturnByTime)
            ? null
            : ParseReturnByTime(request.ReturnByTime);

        var patterns = await db.DriverWorkPatterns
            .Where(p => p.DriverId == driverId)
            .ToListAsync(ct);
        if (patterns.Count == 0)
            db.DriverWorkPatterns.AddRange(DriverWorkPatternHelper.CreateDefaultPatterns(driver));
        else
            DriverWorkPatternHelper.SyncWeekdayPatternsFromProfile(driver, patterns);

        await db.SaveChangesAsync(ct);
        if (previousUserId != request.UserId)
        {
            if (previousUserId.HasValue)
                await DriverUserProvisioning.UnlinkUserAsync(db, previousUserId.Value, ct);
            if (request.UserId.HasValue)
                await SyncLinkedUserRoleAsync(tenantId, request.UserId.Value, ct);
        }

        return (await GetDriverAsync(tenantId, driverId, ct))!;
    }

    public async Task DeleteDriverAsync(Guid tenantId, Guid driverId, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Driver not found.");

        var onOpenRoute = await db.DeliveryRoutes.AnyAsync(r =>
            r.TenantId == tenantId
            && r.DriverId == driverId
            && r.Status != DeliveryRouteStatus.Completed
            && r.Status != DeliveryRouteStatus.Cancelled, ct);
        if (onOpenRoute)
            throw new ArgumentException("This driver is assigned to an open route. Reassign or complete the route first.");

        var onActiveOrder = await db.DeliveryAssignments.AnyAsync(a =>
            a.DriverId == driverId
            && a.DeliveryOrder.TenantId == tenantId
            && a.DeliveryOrder.Status != DeliveryOrderStatus.Delivered
            && a.DeliveryOrder.Status != DeliveryOrderStatus.Cancelled
            && a.DeliveryOrder.Status != DeliveryOrderStatus.Failed, ct);
        if (onActiveOrder)
            throw new ArgumentException("This driver is assigned to an active order. Reassign the order first.");

        db.Drivers.Remove(driver);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DriverWorkPatternDayDto>> GetWorkPatternAsync(
        Guid tenantId,
        Guid driverId,
        CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        currentUser.EnsureDriverSelf(driverId);
        var driver = await db.Drivers.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Driver not found.");

        var patterns = await db.DriverWorkPatterns.AsNoTracking()
            .Where(p => p.DriverId == driverId)
            .ToListAsync(ct);

        if (patterns.Count == 0)
            return MapPatternDtos(DriverWorkPatternHelper.CreateDefaultPatterns(driver));

        return MapPatternDtos(patterns.OrderBy(p => (int)p.DayOfWeek).ToList());
    }

    public async Task<IReadOnlyList<DriverWorkPatternDayDto>> UpdateWorkPatternAsync(
        Guid tenantId,
        Guid driverId,
        IReadOnlyList<UpdateDriverWorkPatternDayRequest> pattern,
        CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Driver not found.");

        if (pattern.Count != 7)
            throw new ArgumentException("Weekly schedule must include all 7 days (Sunday through Saturday).");

        var existing = await db.DriverWorkPatterns.Where(p => p.DriverId == driverId).ToListAsync(ct);
        if (existing.Count == 0)
        {
            existing = DriverWorkPatternHelper.CreateDefaultPatterns(driver).ToList();
            db.DriverWorkPatterns.AddRange(existing);
        }

        var byDay = existing.ToDictionary(p => p.DayOfWeek);
        foreach (var day in DriverWorkPatternHelper.AllDays)
        {
            if (!byDay.ContainsKey(day))
            {
                var row = new DriverWorkPattern { Id = Guid.NewGuid(), DriverId = driverId, DayOfWeek = day };
                db.DriverWorkPatterns.Add(row);
                byDay[day] = row;
            }
        }

        foreach (var request in pattern)
        {
            if (request.DayOfWeek is < 0 or > 6)
                throw new ArgumentException("Day of week must be 0 (Sunday) through 6 (Saturday).");

            var day = (DayOfWeek)request.DayOfWeek;
            var row = byDay[day];
            row.IsWorkingDay = request.IsWorkingDay;

            if (request.IsWorkingDay)
            {
                var schedule = ParseSchedule(
                    request.ShiftStartTime ?? DriverScheduleHelper.FormatShiftTime(row.ShiftStartTime),
                    request.ShiftEndTime ?? DriverScheduleHelper.FormatShiftTime(row.ShiftEndTime),
                    request.LunchMinutes ?? row.LunchMinutes,
                    request.BreakMinutes ?? row.BreakMinutes);
                row.ShiftStartTime = schedule.ShiftStart;
                row.ShiftEndTime = schedule.ShiftEnd;
                row.LunchMinutes = schedule.LunchMinutes;
                row.BreakMinutes = schedule.BreakMinutes;
            }

            row.MaxRouteMinutes = request.MaxRouteMinutes.HasValue
                ? ParseMaxRouteMinutes(request.MaxRouteMinutes)
                : null;
            row.ReturnByTime = string.IsNullOrWhiteSpace(request.ReturnByTime)
                ? null
                : ParseReturnByTime(request.ReturnByTime);
        }

        var monday = byDay[DayOfWeek.Monday];
        if (monday.IsWorkingDay)
        {
            driver.ShiftStartTime = monday.ShiftStartTime;
            driver.ShiftEndTime = monday.ShiftEndTime;
            driver.LunchMinutes = monday.LunchMinutes;
            driver.BreakMinutes = monday.BreakMinutes;
            driver.MaxRouteMinutes = monday.MaxRouteMinutes;
            driver.ReturnByTime = monday.ReturnByTime;
        }

        await db.SaveChangesAsync(ct);
        return await GetWorkPatternAsync(tenantId, driverId, ct);
    }

    public async Task<IReadOnlyList<ResolvedDriverScheduleDto>> GetResolvedScheduleAsync(
        Guid tenantId,
        DateOnly date,
        CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var query = db.Drivers.AsNoTracking().Where(d => d.TenantId == tenantId && d.IsActive);
        if (currentUser.IsDriver && currentUser.DriverId.HasValue)
        {
            query = query.Where(d => d.Id == currentUser.DriverId.Value);
            currentUser.EnsureDriverSelf(currentUser.DriverId.Value);
        }

        var drivers = await query.OrderBy(d => d.DisplayName).ToListAsync(ct);

        var resolved = await scheduleResolver.ResolveAsync(tenantId, date, drivers.Select(d => d.Id).ToList(), ct);

        return drivers.Select(d =>
        {
            resolved.TryGetValue(d.Id, out var day);
            day ??= new ResolvedDriverDay
            {
                DriverId = d.Id,
                Date = date,
                IsWorking = false
            };

            return new ResolvedDriverScheduleDto(
                d.Id,
                d.DisplayName,
                date,
                day.IsWorking,
                day.IsWorking ? DriverScheduleHelper.FormatShiftTime(day.ShiftStart) : null,
                day.IsWorking ? DriverScheduleHelper.FormatShiftTime(day.ShiftEnd) : null,
                day.AvailableWorkMinutes,
                day.MaxRouteMinutes,
                day.ReturnByTime.HasValue ? DriverScheduleHelper.FormatShiftTime(day.ReturnByTime.Value) : null,
                day.Source,
                day.ScheduleExceptionId.HasValue,
                day.ScheduleNote);
        }).ToList();
    }

    public async Task<DriverCalendarDto> GetDriverCalendarAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        if (to < from)
            throw new ArgumentException("End date must be on or after start date.");
        if (from.AddDays(41) < to)
            throw new ArgumentException("Calendar range cannot exceed 42 days.");

        var query = db.Drivers.AsNoTracking().Where(d => d.TenantId == tenantId);
        if (currentUser.IsDriver && currentUser.DriverId.HasValue)
            query = query.Where(d => d.Id == currentUser.DriverId.Value);

        var drivers = await query.OrderBy(d => d.DisplayName).ToListAsync(ct);

        var driverIds = drivers.Select(d => d.Id).ToList();
        var resolved = await scheduleResolver.ResolveRangeAsync(tenantId, from, to, driverIds, ct);

        var fixedRoutes = await db.FixedRouteTemplates.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive && t.DefaultDriverId.HasValue)
            .Select(t => new { t.Id, t.Name, t.DefaultDriverId, t.RouteDays })
            .ToListAsync(ct);

        var fixedRoutesByDriver = fixedRoutes
            .GroupBy(t => t.DefaultDriverId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var assignedRoutes = await db.DeliveryRoutes.AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.ScheduledDate >= from
                && r.ScheduledDate <= to
                && r.DriverId.HasValue
                && r.Status != DeliveryRouteStatus.Cancelled)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Status,
                DriverId = r.DriverId!.Value,
                r.ScheduledDate,
                StopCount = r.Stops.Count
            })
            .ToListAsync(ct);

        var assignedRoutesByDriverDate = assignedRoutes
            .GroupBy(r => (r.DriverId, r.ScheduledDate))
            .ToDictionary(g => g.Key, g => g.ToList());

        var dates = EnumerateDates(from, to).ToList();
        var rows = drivers.Select(driver =>
        {
            resolved.TryGetValue(driver.Id, out var byDate);
            fixedRoutesByDriver.TryGetValue(driver.Id, out var routes);

            var days = dates.Select(date =>
            {
                ResolvedDriverDay? resolvedDay = null;
                if (byDate is not null)
                    byDate.TryGetValue(date, out resolvedDay);
                var day = resolvedDay ?? new ResolvedDriverDay { DriverId = driver.Id, Date = date, IsWorking = false };

                var routeBadges = (routes ?? [])
                    .Where(r => r.RouteDays.Contains(date.DayOfWeek))
                    .Select(r => new DriverCalendarFixedRouteDto(r.Id, r.Name))
                    .ToList();

                assignedRoutesByDriverDate.TryGetValue((driver.Id, date), out var dayRoutes);
                var assigned = (dayRoutes ?? [])
                    .Select(r => new DriverCalendarAssignedRouteDto(
                        r.Id,
                        r.Name,
                        r.Status.ToString(),
                        r.StopCount))
                    .ToList();

                return new DriverCalendarDayDto(
                    date,
                    day.IsWorking,
                    day.IsWorking ? DriverScheduleHelper.FormatShiftTime(day.ShiftStart) : null,
                    day.IsWorking ? DriverScheduleHelper.FormatShiftTime(day.ShiftEnd) : null,
                    day.AvailableWorkMinutes,
                    day.MaxRouteMinutes,
                    day.ReturnByTime.HasValue ? DriverScheduleHelper.FormatShiftTime(day.ReturnByTime.Value) : null,
                    day.Source,
                    day.ScheduleExceptionId.HasValue,
                    day.ScheduleExceptionId,
                    day.ScheduleNote,
                    day.OffBlockStart.HasValue ? DriverScheduleHelper.FormatShiftTime(day.OffBlockStart.Value) : null,
                    day.OffBlockEnd.HasValue ? DriverScheduleHelper.FormatShiftTime(day.OffBlockEnd.Value) : null,
                    routeBadges,
                    assigned);
            }).ToList();

            return new DriverCalendarRowDto(driver.Id, driver.DisplayName, driver.IsActive, days);
        }).ToList();

        return new DriverCalendarDto(from, to, rows);
    }

    public async Task<DriverScheduleExceptionDto> UpsertScheduleExceptionAsync(
        Guid tenantId,
        Guid driverId,
        UpsertDriverScheduleExceptionRequest request,
        CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Driver not found.");

        var existing = await db.DriverScheduleExceptions
            .FirstOrDefaultAsync(e => e.DriverId == driverId && e.Date == request.Date, ct);

        TimeOnly shiftStart;
        TimeOnly shiftEnd;
        int lunchMinutes;
        int breakMinutes;

        if (request.IsWorking)
        {
            var schedule = ParseSchedule(
                request.ShiftStartTime ?? DriverScheduleHelper.FormatShiftTime(driver.ShiftStartTime),
                request.ShiftEndTime ?? DriverScheduleHelper.FormatShiftTime(driver.ShiftEndTime),
                request.LunchMinutes ?? driver.LunchMinutes,
                request.BreakMinutes ?? driver.BreakMinutes);
            shiftStart = schedule.ShiftStart;
            shiftEnd = schedule.ShiftEnd;
            lunchMinutes = schedule.LunchMinutes;
            breakMinutes = schedule.BreakMinutes;
        }
        else
        {
            shiftStart = existing?.ShiftStartTime ?? driver.ShiftStartTime;
            shiftEnd = existing?.ShiftEndTime ?? driver.ShiftEndTime;
            lunchMinutes = existing?.LunchMinutes ?? driver.LunchMinutes;
            breakMinutes = existing?.BreakMinutes ?? driver.BreakMinutes;
        }

        var row = existing ?? new DriverScheduleException
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            Date = request.Date
        };

        row.IsWorking = request.IsWorking;
        row.ShiftStartTime = shiftStart;
        row.ShiftEndTime = shiftEnd;
        row.LunchMinutes = lunchMinutes;
        row.BreakMinutes = breakMinutes;
        row.MaxRouteMinutes = request.MaxRouteMinutes.HasValue
            ? ParseMaxRouteMinutes(request.MaxRouteMinutes)
            : null;
        row.ReturnByTime = string.IsNullOrWhiteSpace(request.ReturnByTime)
            ? null
            : ParseReturnByTime(request.ReturnByTime);
        if (request.IsWorking)
        {
            var (offStart, offEnd) = ParseOffBlock(
                request.OffBlockStartTime, request.OffBlockEndTime, shiftStart, shiftEnd);
            row.OffBlockStartTime = offStart;
            row.OffBlockEndTime = offEnd;
        }
        else
        {
            row.OffBlockStartTime = null;
            row.OffBlockEndTime = null;
        }

        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        row.UpdatedAt = DateTime.UtcNow;

        if (existing is null)
            db.DriverScheduleExceptions.Add(row);

        await db.SaveChangesAsync(ct);
        return MapExceptionDto(row);
    }

    public async Task DeleteScheduleExceptionAsync(
        Guid tenantId,
        Guid driverId,
        DateOnly date,
        CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var driverExists = await db.Drivers.AnyAsync(d => d.Id == driverId && d.TenantId == tenantId, ct);
        if (!driverExists)
            throw new InvalidOperationException("Driver not found.");

        var row = await db.DriverScheduleExceptions
            .FirstOrDefaultAsync(e => e.DriverId == driverId && e.Date == date, ct);
        if (row is null)
            return;

        db.DriverScheduleExceptions.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DriverScheduleExceptionDto>> BulkUpsertScheduleExceptionsAsync(
        Guid tenantId,
        Guid driverId,
        BulkUpsertDriverScheduleExceptionRequest request,
        CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        if (request.To < request.From)
            throw new ArgumentException("End date must be on or after start date.");
        if (request.From.AddDays(90) < request.To)
            throw new ArgumentException("Exception range cannot exceed 90 days.");

        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Driver not found.");

        var dates = EnumerateDates(request.From, request.To).ToList();
        var existing = await db.DriverScheduleExceptions
            .Where(e => e.DriverId == driverId && e.Date >= request.From && e.Date <= request.To)
            .ToDictionaryAsync(e => e.Date, ct);

        TimeOnly shiftStart;
        TimeOnly shiftEnd;
        int lunchMinutes;
        int breakMinutes;

        if (request.IsWorking)
        {
            var schedule = ParseSchedule(
                request.ShiftStartTime ?? DriverScheduleHelper.FormatShiftTime(driver.ShiftStartTime),
                request.ShiftEndTime ?? DriverScheduleHelper.FormatShiftTime(driver.ShiftEndTime),
                request.LunchMinutes ?? driver.LunchMinutes,
                request.BreakMinutes ?? driver.BreakMinutes);
            shiftStart = schedule.ShiftStart;
            shiftEnd = schedule.ShiftEnd;
            lunchMinutes = schedule.LunchMinutes;
            breakMinutes = schedule.BreakMinutes;
        }
        else
        {
            shiftStart = driver.ShiftStartTime;
            shiftEnd = driver.ShiftEndTime;
            lunchMinutes = driver.LunchMinutes;
            breakMinutes = driver.BreakMinutes;
        }

        var maxRoute = request.MaxRouteMinutes.HasValue
            ? ParseMaxRouteMinutes(request.MaxRouteMinutes)
            : null;
        var returnBy = string.IsNullOrWhiteSpace(request.ReturnByTime)
            ? null
            : ParseReturnByTime(request.ReturnByTime);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var now = DateTime.UtcNow;
        var results = new List<DriverScheduleExceptionDto>(dates.Count);

        foreach (var date in dates)
        {
            existing.TryGetValue(date, out var row);
            row ??= new DriverScheduleException
            {
                Id = Guid.NewGuid(),
                DriverId = driverId,
                Date = date
            };

            row.IsWorking = request.IsWorking;
            row.ShiftStartTime = request.IsWorking
                ? shiftStart
                : row.ShiftStartTime == default ? driver.ShiftStartTime : row.ShiftStartTime;
            row.ShiftEndTime = request.IsWorking
                ? shiftEnd
                : row.ShiftEndTime == default ? driver.ShiftEndTime : row.ShiftEndTime;
            row.LunchMinutes = lunchMinutes;
            row.BreakMinutes = breakMinutes;
            row.MaxRouteMinutes = maxRoute;
            row.ReturnByTime = returnBy;
            row.OffBlockStartTime = null;
            row.OffBlockEndTime = null;
            row.Note = note;
            row.UpdatedAt = now;

            if (!existing.ContainsKey(date))
                db.DriverScheduleExceptions.Add(row);

            results.Add(MapExceptionDto(row));
        }

        await db.SaveChangesAsync(ct);
        return results;
    }

    private static DriverScheduleExceptionDto MapExceptionDto(DriverScheduleException row)
    {
        var available = row.IsWorking
            ? DriverSchedule.AvailableWorkMinutes(
                row.ShiftStartTime,
                row.ShiftEndTime,
                row.LunchMinutes,
                row.BreakMinutes,
                row.OffBlockStartTime,
                row.OffBlockEndTime)
            : 0;
        return new DriverScheduleExceptionDto(
            row.Id,
            row.DriverId,
            row.Date,
            row.IsWorking,
            DriverScheduleHelper.FormatShiftTime(row.ShiftStartTime),
            DriverScheduleHelper.FormatShiftTime(row.ShiftEndTime),
            row.LunchMinutes,
            row.BreakMinutes,
            available,
            row.MaxRouteMinutes,
            row.ReturnByTime.HasValue ? DriverScheduleHelper.FormatShiftTime(row.ReturnByTime.Value) : null,
            row.OffBlockStartTime.HasValue ? DriverScheduleHelper.FormatShiftTime(row.OffBlockStartTime.Value) : null,
            row.OffBlockEndTime.HasValue ? DriverScheduleHelper.FormatShiftTime(row.OffBlockEndTime.Value) : null,
            row.Note,
            row.UpdatedAt);
    }

    private static IEnumerable<DateOnly> EnumerateDates(DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
            yield return date;
    }

    private static IReadOnlyList<DriverWorkPatternDayDto> MapPatternDtos(IReadOnlyList<DriverWorkPattern> patterns) =>
        patterns.Select(p =>
        {
            var available = p.IsWorkingDay
                ? DriverScheduleHelper.AvailableWorkMinutes(
                    p.ShiftStartTime, p.ShiftEndTime, p.LunchMinutes, p.BreakMinutes)
                : 0;
            return new DriverWorkPatternDayDto(
                (int)p.DayOfWeek,
                p.IsWorkingDay,
                DriverScheduleHelper.FormatShiftTime(p.ShiftStartTime),
                DriverScheduleHelper.FormatShiftTime(p.ShiftEndTime),
                p.LunchMinutes,
                p.BreakMinutes,
                available,
                p.MaxRouteMinutes,
                p.ReturnByTime.HasValue ? DriverScheduleHelper.FormatShiftTime(p.ReturnByTime.Value) : null);
        }).ToList();

    private async Task<IReadOnlyList<DriverDto>> MapDriversAsync(IReadOnlyList<Driver> drivers, CancellationToken ct)
    {
        if (drivers.Count == 0)
            return [];

        var userIds = drivers.Where(d => d.UserId.HasValue).Select(d => d.UserId!.Value).Distinct().ToList();
        var linkedUserNames = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await userManager.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return drivers.Select(d =>
        {
            var available = DriverScheduleHelper.AvailableWorkMinutes(
                d.ShiftStartTime, d.ShiftEndTime, d.LunchMinutes, d.BreakMinutes);
            return new DriverDto(
                d.Id,
                d.DisplayName,
                d.UserId,
                d.UserId.HasValue && linkedUserNames.TryGetValue(d.UserId.Value, out var name) ? name : null,
                d.IsActive,
                DriverScheduleHelper.FormatShiftTime(d.ShiftStartTime),
                DriverScheduleHelper.FormatShiftTime(d.ShiftEndTime),
                d.LunchMinutes,
                d.BreakMinutes,
                available,
                d.MaxRouteMinutes,
                d.ReturnByTime.HasValue ? DriverScheduleHelper.FormatShiftTime(d.ReturnByTime.Value) : null,
                d.CreatedAt);
        }).ToList();
    }

    private static int? ParseMaxRouteMinutes(int? minutes)
    {
        if (!minutes.HasValue)
            return null;
        if (minutes.Value <= 0)
            throw new ArgumentException("Max route minutes must be positive.");
        return minutes.Value;
    }

    private static TimeOnly? ParseReturnByTime(string? returnBy)
    {
        if (string.IsNullOrWhiteSpace(returnBy))
            return null;
        if (!TimeOnly.TryParse(returnBy.Trim(), out var parsed))
            throw new ArgumentException("Return-by time is invalid.");
        return parsed;
    }

    private static (TimeOnly? Start, TimeOnly? End) ParseOffBlock(
        string? offStart,
        string? offEnd,
        TimeOnly shiftStart,
        TimeOnly shiftEnd)
    {
        var hasStart = !string.IsNullOrWhiteSpace(offStart);
        var hasEnd = !string.IsNullOrWhiteSpace(offEnd);
        if (!hasStart && !hasEnd)
            return (null, null);
        if (hasStart != hasEnd)
            throw new ArgumentException("Off block requires both start and end times, or leave both empty.");

        if (!TimeOnly.TryParse(offStart!.Trim(), out var start) || !TimeOnly.TryParse(offEnd!.Trim(), out var end))
            throw new ArgumentException("Off block times are invalid.");
        if (end <= start)
            throw new ArgumentException("Off block end must be after start.");
        if (start < shiftStart || end > shiftEnd)
            throw new ArgumentException("Off block must fall within the driver's shift.");

        return (start, end);
    }

    private static (TimeOnly ShiftStart, TimeOnly ShiftEnd, int LunchMinutes, int BreakMinutes) ParseSchedule(
        string? shiftStart,
        string? shiftEnd,
        int? lunchMinutes,
        int? breakMinutes)
    {
        var start = DriverScheduleHelper.ParseShiftTime(shiftStart, DriverScheduleHelper.DefaultShiftStart);
        var end = DriverScheduleHelper.ParseShiftTime(shiftEnd, DriverScheduleHelper.DefaultShiftEnd);
        var lunch = lunchMinutes ?? DriverScheduleHelper.DefaultLunchMinutes;
        var breaks = breakMinutes ?? DriverScheduleHelper.DefaultBreakMinutes;

        if (lunch < 0 || breaks < 0)
            throw new ArgumentException("Lunch and break minutes cannot be negative.");
        if (end <= start)
            throw new ArgumentException("Shift end must be after shift start.");
        if (DriverScheduleHelper.AvailableWorkMinutes(start, end, lunch, breaks) <= 0)
            throw new ArgumentException("Shift must allow at least one minute of available work time after lunch and breaks.");

        return (start, end, lunch, breaks);
    }

    private async Task ValidateLinkedUserAsync(
        Guid tenantId,
        Guid? userId,
        Guid? excludeDriverId,
        CancellationToken ct)
    {
        if (!userId.HasValue) return;
        var user = await userManager.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.TenantId == tenantId && u.IsActive, ct);
        if (user is null)
            throw new ArgumentException("Linked user not found in this tenant.");
        if (user.TenantRole == TenantRole.Admin)
            throw new ArgumentException("Tenant administrators cannot be linked as drivers.");

        var linkedElsewhere = await db.Drivers.AsNoTracking()
            .AnyAsync(d =>
                d.TenantId == tenantId
                && d.UserId == userId
                && d.IsActive
                && (!excludeDriverId.HasValue || d.Id != excludeDriverId.Value), ct);
        if (linkedElsewhere)
            throw new ArgumentException("This user is already linked to another driver.");
    }

    private async Task SyncLinkedUserRoleAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Linked user not found in this tenant.");
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId, ct);
        await DriverUserProvisioning.SyncUserAsDriverAsync(userManager, user, tenant.Modules, ct);
    }

    private void EnsureReadAccess(Guid tenantId)
    {
        currentUser.EnsureModule(ProductModule.Delivery);
        currentUser.EnsureTenantAccess(tenantId);
    }

    private void EnsureWriteAccess(Guid tenantId)
    {
        EnsureReadAccess(tenantId);
        currentUser.EnsureTenantAdmin();
    }
}
