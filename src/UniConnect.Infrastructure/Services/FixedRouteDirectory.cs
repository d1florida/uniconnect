using Microsoft.EntityFrameworkCore;
using UniConnect.Application;
using UniConnect.Application.Interfaces;
using UniConnect.Delivery;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Entities;
using UniConnect.Delivery.Enums;
using UniConnect.Delivery.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Insights.Entities;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services;

public class FixedRouteDirectory(AppDbContext db, ICurrentUserService currentUser) : IFixedRouteDirectory
{
    public async Task<IReadOnlyList<DeliveryZoneDto>> GetZonesAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var query = db.DeliveryZones.AsNoTracking().Where(z => z.TenantId == tenantId);
        if (!includeInactive)
            query = query.Where(z => z.IsActive);

        var zones = await query.OrderBy(z => z.Name).ToListAsync(ct);
        var customerCounts = await db.Customers.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.DeliveryZoneId.HasValue)
            .GroupBy(c => c.DeliveryZoneId!.Value)
            .Select(g => new { ZoneId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ZoneId, x => x.Count, ct);

        return zones
            .Select(z => MapZone(z, customerCounts.GetValueOrDefault(z.Id)))
            .ToList();
    }

    public async Task<DeliveryZoneDto> CreateZoneAsync(Guid tenantId, CreateDeliveryZoneRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Zone name is required.");

        var zone = new DeliveryZone
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            MatchType = DeliveryZoneMatchType.Manual,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.DeliveryZones.Add(zone);
        await db.SaveChangesAsync(ct);
        return MapZone(zone, 0);
    }

    public async Task<DeliveryZoneDto> UpdateZoneAsync(Guid tenantId, Guid zoneId, UpdateDeliveryZoneRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == zoneId && z.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Delivery zone not found.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Zone name is required.");

        zone.Name = name;
        zone.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        var customerCount = await db.Customers.CountAsync(c => c.DeliveryZoneId == zoneId, ct);
        return MapZone(zone, customerCount);
    }

    public async Task DeleteZoneAsync(Guid tenantId, Guid zoneId, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == zoneId && z.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Delivery zone not found.");

        var hasTemplate = await db.FixedRouteTemplates.AnyAsync(t => t.DeliveryZoneId == zoneId && t.IsActive, ct);
        if (hasTemplate)
            throw new ArgumentException("Remove or deactivate the fixed route template for this zone first.");

        var hasCustomers = await db.Customers.AnyAsync(c => c.DeliveryZoneId == zoneId, ct);
        if (hasCustomers)
            throw new ArgumentException("Reassign customers to another zone before deleting this zone.");

        zone.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FixedRouteTemplateDto>> GetTemplatesAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var query = db.FixedRouteTemplates.AsNoTracking()
            .Include(t => t.DeliveryZone)
            .Where(t => t.TenantId == tenantId);
        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        var templates = await query.OrderBy(t => t.Name).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var counts = await LoadOrderCountsByTemplateAsync(tenantId, today, ct);
        var depotNames = await db.Depots.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        return templates
            .Select(t => MapTemplate(t, depotNames.GetValueOrDefault(t.DepotId), counts, today))
            .ToList();
    }

    public async Task<FixedRouteTemplateDto?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var template = await db.FixedRouteTemplates.AsNoTracking()
            .Include(t => t.DeliveryZone)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, ct);
        if (template is null)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var counts = await LoadOrderCountsByTemplateAsync(tenantId, today, ct);
        var depotName = await db.Depots.AsNoTracking()
            .Where(d => d.Id == template.DepotId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(ct);
        return MapTemplate(template, depotName, counts, today);
    }

    public async Task<FixedRouteTemplateDto> CreateTemplateAsync(Guid tenantId, CreateFixedRouteTemplateRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.");

        await EnsureZoneAsync(tenantId, request.DeliveryZoneId, ct);
        await EnsureDepotAsync(tenantId, request.DepotId, ct);
        await EnsureOptionalVehicleAsync(tenantId, request.DefaultVehicleId, ct);
        await EnsureOptionalDriverAsync(tenantId, request.DefaultDriverId, ct);

        FixedRouteScheduleHelper.ValidateRouteDays(request.DaysOfWeek);

        var duplicate = await db.FixedRouteTemplates.AnyAsync(
            t => t.TenantId == tenantId && t.DeliveryZoneId == request.DeliveryZoneId && t.IsActive, ct);
        if (duplicate)
            throw new ArgumentException("An active fixed route template already exists for this delivery zone.");

        var template = new FixedRouteTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            DeliveryZoneId = request.DeliveryZoneId,
            RouteDays = FixedRouteScheduleHelper.NormalizeRouteDays(request.DaysOfWeek).ToList(),
            DepotId = request.DepotId,
            DefaultVehicleId = request.DefaultVehicleId,
            DefaultDriverId = request.DefaultDriverId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.FixedRouteTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        var loaded = await db.FixedRouteTemplates.AsNoTracking()
            .Include(t => t.DeliveryZone)
            .FirstAsync(t => t.Id == template.Id, ct);
        var depotName = await db.Depots.AsNoTracking()
            .Where(d => d.Id == loaded.DepotId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var counts = await LoadOrderCountsByTemplateAsync(tenantId, today, ct);
        return MapTemplate(loaded, depotName, counts, today);
    }

    public async Task<FixedRouteTemplateDto> UpdateTemplateAsync(Guid tenantId, Guid templateId, UpdateFixedRouteTemplateRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var template = await db.FixedRouteTemplates
            .Include(t => t.DeliveryZone)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Fixed route template not found.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.");

        await EnsureDepotAsync(tenantId, request.DepotId, ct);
        await EnsureOptionalVehicleAsync(tenantId, request.DefaultVehicleId, ct);
        await EnsureOptionalDriverAsync(tenantId, request.DefaultDriverId, ct);

        FixedRouteScheduleHelper.ValidateRouteDays(request.DaysOfWeek);
        var normalizedDays = FixedRouteScheduleHelper.NormalizeRouteDays(request.DaysOfWeek).ToList();
        var dayChanged = !template.RouteDays.SequenceEqual(normalizedDays);
        template.Name = name;
        template.RouteDays = normalizedDays;
        template.DepotId = request.DepotId;
        template.DefaultVehicleId = request.DefaultVehicleId;
        template.DefaultDriverId = request.DefaultDriverId;
        template.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        if (dayChanged)
            await RefreshHoldsForTemplateAsync(template.Id, ct);

        var depotName = await db.Depots.AsNoTracking()
            .Where(d => d.Id == template.DepotId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var counts = await LoadOrderCountsByTemplateAsync(tenantId, today, ct);
        return MapTemplate(template, depotName, counts, today);
    }

    public async Task DeleteTemplateAsync(Guid tenantId, Guid templateId, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var template = await db.FixedRouteTemplates.FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Fixed route template not found.");

        template.IsActive = false;
        await db.SaveChangesAsync(ct);

        var heldOrders = await db.DeliveryOrders
            .Where(o => o.TenantId == tenantId
                && o.Status == DeliveryOrderStatus.Created
                && o.FixedRouteTemplateId == templateId)
            .ToListAsync(ct);
        foreach (var order in heldOrders)
            ClearHoldFromOrder(order);

        await db.SaveChangesAsync(ct);
    }

    public async Task ApplyHoldToOrderAsync(DeliveryOrder order, Guid customerId, CancellationToken ct = default)
    {
        if (order.Status != DeliveryOrderStatus.Created)
            return;

        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == order.TenantId, ct);
        if (customer is null)
        {
            ClearHoldFromOrder(order);
            return;
        }

        if (!customer.DeliveryZoneId.HasValue)
        {
            ClearHoldFromOrder(order);
            return;
        }

        var template = await db.FixedRouteTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.TenantId == order.TenantId
                && t.DeliveryZoneId == customer.DeliveryZoneId.Value
                && t.IsActive, ct);

        if (template is null)
        {
            ClearHoldFromOrder(order);
            return;
        }

        order.FixedRouteTemplateId = template.Id;
        order.HeldUntil = FixedRouteScheduleHelper.ComputeNextRouteDate(
            template.RouteDays,
            DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task RefreshHoldsForCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId, ct);
        if (customer is null)
            return;

        var orders = await db.DeliveryOrders
            .Where(o => o.TenantId == tenantId
                && o.CustomerId == customerId
                && o.Status == DeliveryOrderStatus.Created)
            .ToListAsync(ct);

        foreach (var order in orders)
            await ApplyHoldToOrderAsync(order, customerId, ct);

        await db.SaveChangesAsync(ct);
    }

    public void ClearHoldFromOrder(DeliveryOrder order)
    {
        order.FixedRouteTemplateId = null;
        order.HeldUntil = null;
    }

    private async Task RefreshHoldsForTemplateAsync(Guid templateId, CancellationToken ct)
    {
        var template = await db.FixedRouteTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null)
            return;

        var orders = await db.DeliveryOrders
            .Where(o => o.FixedRouteTemplateId == templateId && o.Status == DeliveryOrderStatus.Created)
            .ToListAsync(ct);

        foreach (var order in orders)
        {
            order.HeldUntil = FixedRouteScheduleHelper.ComputeNextRouteDate(
                template.RouteDays,
                DateOnly.FromDateTime(DateTime.UtcNow));
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<Guid, (int Held, int Due)>> LoadOrderCountsByTemplateAsync(
        Guid tenantId,
        DateOnly today,
        CancellationToken ct)
    {
        var rows = await db.DeliveryOrders.AsNoTracking()
            .Where(o => o.TenantId == tenantId
                && o.Status == DeliveryOrderStatus.Created
                && o.FixedRouteTemplateId.HasValue)
            .GroupBy(o => o.FixedRouteTemplateId!.Value)
            .Select(g => new
            {
                TemplateId = g.Key,
                Held = g.Count(),
                Due = g.Count(o => o.HeldUntil.HasValue && o.HeldUntil.Value <= today)
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.TemplateId, r => (r.Held, r.Due));
    }

    private static DeliveryZoneDto MapZone(DeliveryZone zone, int customerCount) =>
        new(
            zone.Id,
            zone.TenantId,
            zone.Name,
            zone.MatchType.ToString(),
            zone.IsActive,
            customerCount,
            zone.CreatedAt);

    private static FixedRouteTemplateDto MapTemplate(
        FixedRouteTemplate template,
        string? depotName,
        IReadOnlyDictionary<Guid, (int Held, int Due)> counts,
        DateOnly today)
    {
        var (held, due) = counts.GetValueOrDefault(template.Id);
        var routeDays = FixedRouteScheduleHelper.NormalizeRouteDays(template.RouteDays);
        var nextRoute = FixedRouteScheduleHelper.ComputeNextRouteDate(routeDays, today);
        return new FixedRouteTemplateDto(
            template.Id,
            template.TenantId,
            template.Name,
            template.DeliveryZoneId,
            template.DeliveryZone.Name,
            routeDays.Select(FixedRouteScheduleHelper.FormatDayOfWeek).ToList(),
            FixedRouteScheduleHelper.FormatDaysOfWeek(routeDays),
            template.DepotId,
            depotName,
            template.DefaultVehicleId,
            template.DefaultDriverId,
            template.IsActive,
            held,
            due,
            nextRoute.ToString("yyyy-MM-dd"),
            template.CreatedAt);
    }

    private async Task EnsureZoneAsync(Guid tenantId, Guid zoneId, CancellationToken ct)
    {
        if (!await db.DeliveryZones.AnyAsync(z => z.Id == zoneId && z.TenantId == tenantId && z.IsActive, ct))
            throw new ArgumentException("Delivery zone not found.");
    }

    private async Task EnsureDepotAsync(Guid tenantId, Guid depotId, CancellationToken ct)
    {
        if (!await db.Depots.AnyAsync(d => d.Id == depotId && d.TenantId == tenantId && d.IsActive, ct))
            throw new ArgumentException("Depot not found.");
    }

    private async Task EnsureOptionalVehicleAsync(Guid tenantId, Guid? vehicleId, CancellationToken ct)
    {
        if (!vehicleId.HasValue)
            return;

        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId.Value && v.TenantId == tenantId, ct))
            throw new ArgumentException("Vehicle not found.");
    }

    private async Task EnsureOptionalDriverAsync(Guid tenantId, Guid? driverId, CancellationToken ct)
    {
        if (!driverId.HasValue)
            return;

        if (!await db.Drivers.AnyAsync(d => d.Id == driverId.Value && d.TenantId == tenantId && d.IsActive, ct))
            throw new ArgumentException("Driver not found.");
    }

    private void EnsureReadAccess(Guid tenantId)
    {
        currentUser.EnsureTenantAccess(tenantId);
        currentUser.EnsureModule(ProductModule.Delivery);
    }

    private void EnsureWriteAccess(Guid tenantId)
    {
        EnsureReadAccess(tenantId);
        if (currentUser.TenantRole is TenantRole.Operator or TenantRole.Driver)
            throw new UnauthorizedAccessException("Tenant admin access is required.");
    }
}
