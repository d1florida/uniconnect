using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Identity;
using UniConnect.Insights.Entities;
using UniConnect.Insights;
using UniConnect.Tenant;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services.Insights;

internal static class DriverUserProvisioning
{
    public static async Task<Driver> EnsureDriverForUserAsync(
        AppDbContext db,
        Guid tenantId,
        ApplicationUser user,
        CancellationToken ct = default)
    {
        var existing = await db.Drivers
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.UserId == user.Id, ct);
        if (existing is not null)
            return existing;

        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DisplayName = user.DisplayName,
            UserId = user.Id,
            IsActive = true,
            ShiftStartTime = DriverScheduleHelper.DefaultShiftStart,
            ShiftEndTime = DriverScheduleHelper.DefaultShiftEnd,
            LunchMinutes = DriverScheduleHelper.DefaultLunchMinutes,
            BreakMinutes = DriverScheduleHelper.DefaultBreakMinutes,
            CreatedAt = DateTime.UtcNow
        };
        db.Drivers.Add(driver);
        db.DriverWorkPatterns.AddRange(DriverWorkPatternHelper.CreateDefaultPatterns(driver));
        await db.SaveChangesAsync(ct);
        return driver;
    }

    public static async Task UnlinkUserAsync(AppDbContext db, Guid userId, CancellationToken ct = default)
    {
        var drivers = await db.Drivers.Where(d => d.UserId == userId).ToListAsync(ct);
        foreach (var driver in drivers)
            driver.UserId = null;
        if (drivers.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    public static async Task SyncUserAsDriverAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        ProductModule tenantModules,
        CancellationToken ct = default)
    {
        user.TenantRole = TenantRole.Driver;
        user.ModuleAccess = ProductModuleHelper.EffectiveModules(ProductModule.Delivery, tenantModules);
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public static ProductModule DriverModuleAccess(ProductModule tenantModules) =>
        ProductModuleHelper.EffectiveModules(ProductModule.Delivery, tenantModules);
}
