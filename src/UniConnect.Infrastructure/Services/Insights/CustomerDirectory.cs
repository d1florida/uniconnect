using Microsoft.EntityFrameworkCore;
using UniConnect.Application;
using UniConnect.Application.Interfaces;
using UniConnect.Delivery.Enums;
using UniConnect.Delivery.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Insights;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;
using UniConnect.Insights.Interfaces;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services.Insights;

public class CustomerDirectory(
    AppDbContext db,
    ICurrentUserService currentUser,
    IGeocodingService geocoding,
    IFixedRouteDirectory fixedRoutes) : ICustomerDirectory
{
    public async Task<Customer> GetOrCreateAsync(
        Guid tenantId,
        string name,
        string? phone,
        string? deliveryAddress = null,
        CancellationToken ct = default)
    {
        var trimmedName = name.Trim();
        var trimmedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new ArgumentException("Customer name is required.");

        var existing = await db.Customers
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId && c.Name == trimmedName && c.Phone == trimmedPhone,
                ct);

        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(deliveryAddress) && string.IsNullOrWhiteSpace(existing.DeliveryAddress))
            {
                existing.DeliveryAddress = deliveryAddress.Trim();
                await ApplyGeocodeAsync(existing, ct);
                await db.SaveChangesAsync(ct);
            }

            return existing;
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = trimmedName,
            Phone = trimmedPhone,
            DeliveryAddress = string.IsNullOrWhiteSpace(deliveryAddress) ? null : deliveryAddress.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(customer.DeliveryAddress))
            await ApplyGeocodeAsync(customer, ct);

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var query = db.Customers.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        var customers = await query
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var zoneNames = await LoadZoneNamesAsync(tenantId, customers, ct);
        return customers.Select(c => Map(c, ResolveZoneName(c.DeliveryZoneId, zoneNames))).ToList();
    }

    public async Task<CustomerDto?> GetCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId, ct);
        if (customer is null)
            return null;

        var zoneNames = await LoadZoneNamesAsync(tenantId, [customer], ct);
        return Map(customer, ResolveZoneName(customer.DeliveryZoneId, zoneNames));
    }

    public async Task<CustomerDto> CreateCustomerAsync(Guid tenantId, CreateCustomerRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name is required.");

        var deliveryAddress = string.IsNullOrWhiteSpace(request.DeliveryAddress) ? null : request.DeliveryAddress.Trim();
        var (windowStart, windowEnd) = ParseDeliveryWindow(request.DeliveryWindowStart, request.DeliveryWindowEnd);
        var (noDeliveryStart, noDeliveryEnd) = ParseNoDeliveryHours(request.NoDeliveryStart, request.NoDeliveryEnd);
        await EnsureDeliveryZoneAsync(tenantId, request.DeliveryZoneId, ct);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            ExternalRef = string.IsNullOrWhiteSpace(request.ExternalRef) ? null : request.ExternalRef.Trim(),
            DeliveryAddress = deliveryAddress,
            DeliveryHours = string.IsNullOrWhiteSpace(request.DeliveryHours) ? null : request.DeliveryHours.Trim(),
            DeliveryWindowStart = windowStart,
            DeliveryWindowEnd = windowEnd,
            NoDeliveryStart = noDeliveryStart,
            NoDeliveryEnd = noDeliveryEnd,
            DeliveryZoneId = request.DeliveryZoneId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(deliveryAddress))
            await ApplyGeocodeAsync(customer, ct);

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        await fixedRoutes.RefreshHoldsForCustomerAsync(tenantId, customer.Id, ct);

        var zoneNames = await LoadZoneNamesAsync(tenantId, [customer], ct);
        return Map(customer, ResolveZoneName(customer.DeliveryZoneId, zoneNames));
    }

    public async Task<CustomerDto> UpdateCustomerAsync(Guid tenantId, Guid customerId, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Customer not found.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Customer name is required.");

        var deliveryAddress = string.IsNullOrWhiteSpace(request.DeliveryAddress) ? null : request.DeliveryAddress.Trim();
        var addressChanged = deliveryAddress is not null
            && !string.Equals(
                geocoding.NormalizeAddress(customer.DeliveryAddress ?? string.Empty),
                geocoding.NormalizeAddress(deliveryAddress),
                StringComparison.Ordinal);

        var (windowStart, windowEnd) = ParseDeliveryWindow(
            request.DeliveryWindowStart ?? FormatWindow(customer.DeliveryWindowStart),
            request.DeliveryWindowEnd ?? FormatWindow(customer.DeliveryWindowEnd));
        var (noDeliveryStart, noDeliveryEnd) = ParseNoDeliveryHours(
            request.NoDeliveryStart ?? FormatWindow(customer.NoDeliveryStart),
            request.NoDeliveryEnd ?? FormatWindow(customer.NoDeliveryEnd));
        await EnsureDeliveryZoneAsync(tenantId, request.DeliveryZoneId, ct);

        customer.Name = name;
        customer.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        customer.ExternalRef = string.IsNullOrWhiteSpace(request.ExternalRef) ? null : request.ExternalRef.Trim();
        customer.DeliveryAddress = deliveryAddress;
        customer.DeliveryHours = string.IsNullOrWhiteSpace(request.DeliveryHours) ? null : request.DeliveryHours.Trim();
        customer.DeliveryWindowStart = windowStart;
        customer.DeliveryWindowEnd = windowEnd;
        customer.NoDeliveryStart = noDeliveryStart;
        customer.NoDeliveryEnd = noDeliveryEnd;
        customer.DeliveryZoneId = request.DeliveryZoneId;
        customer.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        customer.IsActive = request.IsActive;

        if (addressChanged)
        {
            customer.DeliveryLatitude = null;
            customer.DeliveryLongitude = null;
            customer.DeliveryFormattedAddress = null;
            if (!string.IsNullOrWhiteSpace(deliveryAddress))
                await ApplyGeocodeAsync(customer, ct, forceRefresh: true);
        }
        else if (string.IsNullOrWhiteSpace(deliveryAddress))
        {
            customer.DeliveryLatitude = null;
            customer.DeliveryLongitude = null;
            customer.DeliveryFormattedAddress = null;
        }

        await db.SaveChangesAsync(ct);
        await fixedRoutes.RefreshHoldsForCustomerAsync(tenantId, customer.Id, ct);

        var zoneNames = await LoadZoneNamesAsync(tenantId, [customer], ct);
        return Map(customer, ResolveZoneName(customer.DeliveryZoneId, zoneNames));
    }

    public async Task SyncExternalRefAsync(Guid tenantId, Guid customerId, string? externalRef, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var trimmed = string.IsNullOrWhiteSpace(externalRef) ? null : externalRef.Trim();

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId && c.IsActive, ct);
        if (customer is null)
            return;

        if (string.Equals(customer.ExternalRef, trimmed, StringComparison.Ordinal))
            return;

        customer.ExternalRef = trimmed;
        await db.SaveChangesAsync(ct);
    }

    public async Task SyncDeliveryAddressAsync(Guid tenantId, Guid customerId, string deliveryAddress, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var address = deliveryAddress.Trim();
        if (string.IsNullOrWhiteSpace(address))
            return;

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId && c.IsActive, ct);
        if (customer is null)
            return;

        if (string.Equals(customer.DeliveryAddress?.Trim(), address, StringComparison.OrdinalIgnoreCase))
            return;

        customer.DeliveryAddress = address;
        customer.DeliveryLatitude = null;
        customer.DeliveryLongitude = null;
        customer.DeliveryFormattedAddress = null;
        await ApplyGeocodeAsync(customer, ct, forceRefresh: true);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Customer not found.");

        var onActiveOrder = await db.DeliveryOrders.AnyAsync(o =>
            o.CustomerId == customerId
            && o.TenantId == tenantId
            && o.Status != DeliveryOrderStatus.Delivered
            && o.Status != DeliveryOrderStatus.Cancelled
            && o.Status != DeliveryOrderStatus.Failed, ct);
        if (onActiveOrder)
            throw new ArgumentException("This customer has active orders. Complete or cancel them first.");

        customer.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyGeocodeAsync(Customer customer, CancellationToken ct, bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(customer.DeliveryAddress))
            return;

        var geocoded = await geocoding.GeocodeAsync(customer.DeliveryAddress, ct, forceRefresh, tenantId: customer.TenantId);
        if (geocoded.HasValue)
        {
            customer.DeliveryLatitude = geocoded.Value.Latitude;
            customer.DeliveryLongitude = geocoded.Value.Longitude;
            customer.DeliveryFormattedAddress = geocoded.Value.FormattedAddress;
        }
    }

    private static (TimeOnly? Start, TimeOnly? End) ParseDeliveryWindow(string? start, string? end)
    {
        var hasStart = !string.IsNullOrWhiteSpace(start);
        var hasEnd = !string.IsNullOrWhiteSpace(end);
        if (!hasStart && !hasEnd)
            return (null, null);

        if (!hasStart || !hasEnd)
            throw new ArgumentException("Set both open hours start and end, or leave both empty.");

        var windowStart = DriverScheduleHelper.ParseShiftTime(start, DriverScheduleHelper.DefaultShiftStart);
        var windowEnd = DriverScheduleHelper.ParseShiftTime(end, DriverScheduleHelper.DefaultShiftEnd);
        if (windowEnd <= windowStart)
            throw new ArgumentException("Open hours end must be after start.");

        return (windowStart, windowEnd);
    }

    private static (TimeOnly? Start, TimeOnly? End) ParseNoDeliveryHours(string? start, string? end)
    {
        var hasStart = !string.IsNullOrWhiteSpace(start);
        var hasEnd = !string.IsNullOrWhiteSpace(end);
        if (!hasStart && !hasEnd)
            return (null, null);

        if (!hasStart || !hasEnd)
            throw new ArgumentException("Set both no-delivery start and end, or leave both empty.");

        var blockStart = DriverScheduleHelper.ParseShiftTime(start, DriverScheduleHelper.DefaultShiftStart);
        var blockEnd = DriverScheduleHelper.ParseShiftTime(end, DriverScheduleHelper.DefaultShiftEnd);
        if (blockEnd <= blockStart)
            throw new ArgumentException("No-delivery end must be after start.");

        return (blockStart, blockEnd);
    }

    private static string? FormatWindow(TimeOnly? time) =>
        time.HasValue ? DriverScheduleHelper.FormatShiftTime(time.Value) : null;

    internal static CustomerDto Map(Customer customer, string? deliveryZoneName = null) => new(
        customer.Id,
        customer.Name,
        customer.Phone,
        customer.ExternalRef,
        customer.DeliveryAddress,
        customer.DeliveryLatitude,
        customer.DeliveryLongitude,
        customer.DeliveryHours,
        FormatWindow(customer.DeliveryWindowStart),
        FormatWindow(customer.DeliveryWindowEnd),
        FormatWindow(customer.NoDeliveryStart),
        FormatWindow(customer.NoDeliveryEnd),
        customer.DeliveryZoneId,
        deliveryZoneName,
        customer.Notes,
        customer.IsActive,
        customer.CreatedAt);

    private async Task EnsureDeliveryZoneAsync(Guid tenantId, Guid? zoneId, CancellationToken ct)
    {
        if (!zoneId.HasValue)
            return;

        if (!await db.DeliveryZones.AnyAsync(z => z.Id == zoneId.Value && z.TenantId == tenantId && z.IsActive, ct))
            throw new ArgumentException("Delivery zone not found.");
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadZoneNamesAsync(
        Guid tenantId,
        IReadOnlyList<Customer> customers,
        CancellationToken ct)
    {
        var zoneIds = customers
            .Where(c => c.DeliveryZoneId.HasValue)
            .Select(c => c.DeliveryZoneId!.Value)
            .Distinct()
            .ToList();
        if (zoneIds.Count == 0)
            return new Dictionary<Guid, string>();

        return await db.DeliveryZones.AsNoTracking()
            .Where(z => z.TenantId == tenantId && zoneIds.Contains(z.Id))
            .ToDictionaryAsync(z => z.Id, z => z.Name, ct);
    }

    private static string? ResolveZoneName(Guid? zoneId, IReadOnlyDictionary<Guid, string> zoneNames) =>
        zoneId.HasValue ? zoneNames.GetValueOrDefault(zoneId.Value) : null;

    private void EnsureReadAccess(Guid tenantId)
    {
        currentUser.EnsureModule(ProductModule.Delivery);
        currentUser.EnsureTenantAccess(tenantId);
    }

    private void EnsureWriteAccess(Guid tenantId)
    {
        EnsureReadAccess(tenantId);
        if (!currentUser.IsPlatformAdmin && !currentUser.IsTenantAdmin)
            throw new ForbiddenException("Tenant administrator access is required.");
    }
}
