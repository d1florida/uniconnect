using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.Delivery.Enums;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Identity;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;
using UniConnect.Insights.Interfaces;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services.Insights;

public class DriverDirectory(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser) : IDriverDirectory
{
    public async Task<IReadOnlyList<DriverDto>> GetDriversAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var drivers = await db.Drivers.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderBy(d => d.DisplayName)
            .ToListAsync(ct);

        return await MapDriversAsync(drivers, ct);
    }

    public async Task<DriverDto?> GetDriverAsync(Guid tenantId, Guid driverId, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
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

        await ValidateLinkedUserAsync(tenantId, request.UserId, ct);

        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DisplayName = displayName,
            UserId = request.UserId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Drivers.Add(driver);
        await db.SaveChangesAsync(ct);
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

        await ValidateLinkedUserAsync(tenantId, request.UserId, ct);

        driver.DisplayName = displayName;
        driver.UserId = request.UserId;
        driver.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
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

        return drivers.Select(d => new DriverDto(
            d.Id,
            d.DisplayName,
            d.UserId,
            d.UserId.HasValue && linkedUserNames.TryGetValue(d.UserId.Value, out var name) ? name : null,
            d.IsActive,
            d.CreatedAt)).ToList();
    }

    private async Task ValidateLinkedUserAsync(Guid tenantId, Guid? userId, CancellationToken ct)
    {
        if (!userId.HasValue) return;
        var exists = await userManager.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId.Value && u.TenantId == tenantId && u.IsActive, ct);
        if (!exists)
            throw new ArgumentException("Linked user not found in this tenant.");
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
