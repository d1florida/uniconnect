using Microsoft.EntityFrameworkCore;
using UniConnect.Application;
using UniConnect.Application.Interfaces;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Entities;
using UniConnect.Delivery.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services;

public class DepotDirectory(
    AppDbContext db,
    ICurrentUserService currentUser,
    IGeocodingService geocoding) : IDepotDirectory
{
    public async Task<IReadOnlyList<DepotDto>> GetDepotsAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var query = db.Depots.AsNoTracking().Where(d => d.TenantId == tenantId);
        if (!includeInactive)
            query = query.Where(d => d.IsActive);

        var depots = await query
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.Name)
            .ToListAsync(ct);

        return depots.Select(Map).ToList();
    }

    public async Task<DepotDto?> GetDepotAsync(Guid tenantId, Guid depotId, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var depot = await db.Depots.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == depotId && d.TenantId == tenantId, ct);
        return depot is null ? null : Map(depot);
    }

    public async Task<DepotDto> CreateDepotAsync(Guid tenantId, CreateDepotRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var name = request.Name.Trim();
        var address = request.Address.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Depot name is required.");
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Depot address is required.");

        var depot = new Depot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Address = address,
            Hours = string.IsNullOrWhiteSpace(request.Hours) ? null : request.Hours.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsDefault = request.IsDefault,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await ApplyGeocodeAsync(depot, ct);
        if (request.IsDefault)
            await ClearDefaultExceptAsync(tenantId, depot.Id, ct);

        db.Depots.Add(depot);
        await db.SaveChangesAsync(ct);
        return Map(depot);
    }

    public async Task<DepotDto> UpdateDepotAsync(Guid tenantId, Guid depotId, UpdateDepotRequest request, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var depot = await db.Depots.FirstOrDefaultAsync(d => d.Id == depotId && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Depot not found.");

        var name = request.Name.Trim();
        var address = request.Address.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Depot name is required.");
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Depot address is required.");

        var addressChanged = !string.Equals(
            geocoding.NormalizeAddress(depot.Address),
            geocoding.NormalizeAddress(address),
            StringComparison.Ordinal);
        depot.Name = name;
        depot.Address = address;
        depot.Hours = string.IsNullOrWhiteSpace(request.Hours) ? null : request.Hours.Trim();
        depot.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        depot.IsDefault = request.IsDefault;
        depot.IsActive = request.IsActive;

        if (addressChanged)
        {
            depot.Latitude = null;
            depot.Longitude = null;
            await ApplyGeocodeAsync(depot, ct, forceRefresh: true);
        }

        if (request.IsDefault)
            await ClearDefaultExceptAsync(tenantId, depot.Id, ct);

        await db.SaveChangesAsync(ct);
        return Map(depot);
    }

    public async Task DeleteDepotAsync(Guid tenantId, Guid depotId, CancellationToken ct = default)
    {
        EnsureWriteAccess(tenantId);
        var depot = await db.Depots.FirstOrDefaultAsync(d => d.Id == depotId && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Depot not found.");

        depot.IsActive = false;
        depot.IsDefault = false;

        var vehicles = await db.Vehicles.Where(v => v.HomeDepotId == depotId).ToListAsync(ct);
        foreach (var vehicle in vehicles)
            vehicle.HomeDepotId = null;

        await db.SaveChangesAsync(ct);
    }

    public async Task<string> ResolveDepotAddressAsync(Guid tenantId, Guid? depotId, string? depotAddress, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);

        if (depotId.HasValue)
        {
            var depot = await db.Depots.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == depotId.Value && d.TenantId == tenantId && d.IsActive, ct)
                ?? throw new ArgumentException("Depot not found.");
            return depot.Address;
        }

        if (string.IsNullOrWhiteSpace(depotAddress))
        {
            var fallback = await db.Depots.AsNoTracking()
                .Where(d => d.TenantId == tenantId && d.IsActive && d.IsDefault)
                .Select(d => d.Address)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            throw new ArgumentException("Select a depot or enter a depot address.");
        }

        return depotAddress.Trim();
    }

    private async Task ApplyGeocodeAsync(Depot depot, CancellationToken ct, bool forceRefresh = false)
    {
        var geocoded = await geocoding.GeocodeAsync(depot.Address, ct, forceRefresh, tenantId: depot.TenantId);
        if (geocoded.HasValue)
        {
            depot.Latitude = geocoded.Value.Latitude;
            depot.Longitude = geocoded.Value.Longitude;
        }
    }

    private async Task ClearDefaultExceptAsync(Guid tenantId, Guid depotId, CancellationToken ct)
    {
        var others = await db.Depots
            .Where(d => d.TenantId == tenantId && d.Id != depotId && d.IsDefault)
            .ToListAsync(ct);
        foreach (var other in others)
            other.IsDefault = false;
    }

    private static DepotDto Map(Depot depot) => new(
        depot.Id,
        depot.TenantId,
        depot.Name,
        depot.Address,
        depot.Latitude,
        depot.Longitude,
        depot.IsDefault,
        depot.Hours,
        depot.Notes,
        depot.IsActive,
        depot.CreatedAt);

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
