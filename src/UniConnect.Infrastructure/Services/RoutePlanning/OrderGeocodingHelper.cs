using Microsoft.EntityFrameworkCore;
using UniConnect.Delivery.Entities;
using UniConnect.Infrastructure.Data;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.RoutePlanning.Routing;

namespace UniConnect.Infrastructure.Services.RoutePlanning;

public sealed class OrderGeocodingHelper(AppDbContext db, IGeocodingService geocoding)
{
    public async Task EnsureOrderCoordinatesAsync(
        DeliveryOrder order,
        CancellationToken ct = default,
        bool upgradeDeterministic = false)
    {
        var pickupHints = AddressGeocodeHints.Parse(order.PickupAddress);
        var deliveryHints = AddressGeocodeHints.Parse(order.DeliveryAddress);
        var sharedHints = AddressGeocodeHints.Merge(pickupHints, deliveryHints);

        var changed = false;
        changed |= await EnsureEndpointAsync(
            order.PickupAddress,
            AddressGeocodeHints.Merge(sharedHints, pickupHints),
            order.PickupLatitude,
            order.PickupLongitude,
            (lat, lng) =>
            {
                order.PickupLatitude = lat;
                order.PickupLongitude = lng;
            },
            upgradeDeterministic,
            ct);
        changed |= await EnsureEndpointAsync(
            order.DeliveryAddress,
            AddressGeocodeHints.Merge(sharedHints, deliveryHints),
            order.DeliveryLatitude,
            order.DeliveryLongitude,
            (lat, lng) =>
            {
                order.DeliveryLatitude = lat;
                order.DeliveryLongitude = lng;
            },
            upgradeDeterministic,
            ct);

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    public async Task EnsureOrdersCoordinatesAsync(IEnumerable<DeliveryOrder> orders, CancellationToken ct = default)
    {
        foreach (var order in orders)
            await EnsureOrderCoordinatesAsync(order, ct);
    }

    public async Task EnsureOrdersCoordinatesAsync(IEnumerable<Guid> orderIds, CancellationToken ct = default)
    {
        var ids = orderIds.ToList();
        if (ids.Count == 0)
            return;

        var orders = await db.DeliveryOrders.Where(o => ids.Contains(o.Id)).ToListAsync(ct);
        await EnsureOrdersCoordinatesAsync(orders, ct);
    }

    public async Task<bool> HasLowQualityGeocodeAsync(string address, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return true;

        var source = await GetCachedSourceAsync(address, ct);
        if (source is null)
            return true;

        return geocoding.ShouldUpgradeCachedSource(source, address);
    }

    public async Task<bool> CoordinatesOutOfSyncWithCacheAsync(
        string address,
        decimal? latitude,
        decimal? longitude,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address) || latitude is null || longitude is null)
            return false;

        var normalized = NormalizeAddress(address);
        var cached = await db.GeocodedAddresses.AsNoTracking()
            .Where(g => g.NormalizedAddress == normalized)
            .Select(g => new { g.Latitude, g.Longitude })
            .FirstOrDefaultAsync(ct);

        if (cached is null)
            return false;

        const decimal tolerance = 0.0005m;
        return Math.Abs(cached.Latitude - latitude.Value) > tolerance
            || Math.Abs(cached.Longitude - longitude.Value) > tolerance;
    }

    public async Task<bool> HasDeterministicGeocodeAsync(string address, CancellationToken ct = default)
    {
        var source = await GetCachedSourceAsync(address, ct);
        return source?.Equals("deterministic", StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<string?> GetCachedSourceAsync(string address, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        var normalized = geocoding.NormalizeAddress(address);
        return await db.GeocodedAddresses.AsNoTracking()
            .Where(g => g.NormalizedAddress == normalized)
            .Select(g => g.Source)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetCachedSourcesAsync(
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var normalized = addresses
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(geocoding.NormalizeAddress)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var rows = await db.GeocodedAddresses.AsNoTracking()
            .Where(g => normalized.Contains(g.NormalizedAddress))
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.NormalizedAddress, r => r.Source, StringComparer.Ordinal);
    }

    public string? ResolveCachedSource(string address, IReadOnlyDictionary<string, string> sourcesByNormalized)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        return sourcesByNormalized.GetValueOrDefault(NormalizeAddress(address));
    }

    private string NormalizeAddress(string address) => geocoding.NormalizeAddress(address);

    public Task<GeoPoint?> GeocodeAddressAsync(string address, CancellationToken ct = default, AddressGeocodeHints hints = default) =>
        geocoding.GeocodeAsync(address, ct, hints: hints);

    public GeoPoint? PickupPoint(DeliveryOrder order) =>
        order.PickupLatitude.HasValue && order.PickupLongitude.HasValue
            ? new GeoPoint(order.PickupLatitude.Value, order.PickupLongitude.Value)
            : null;

    public GeoPoint? DeliveryPoint(DeliveryOrder order) =>
        order.DeliveryLatitude.HasValue && order.DeliveryLongitude.HasValue
            ? new GeoPoint(order.DeliveryLatitude.Value, order.DeliveryLongitude.Value)
            : null;

    private async Task<bool> EnsureEndpointAsync(
        string address,
        AddressGeocodeHints hints,
        decimal? latitude,
        decimal? longitude,
        Action<decimal, decimal> apply,
        bool upgradeDeterministic,
        CancellationToken ct)
    {
        var missing = !latitude.HasValue || !longitude.HasValue;
        var lowQuality = await HasLowQualityGeocodeAsync(address, ct);
        var outOfSync = !missing
            && await CoordinatesOutOfSyncWithCacheAsync(address, latitude, longitude, ct);
        var forceRefresh = (missing && upgradeDeterministic)
            || outOfSync
            || (!missing && upgradeDeterministic && lowQuality);
        if (!missing && !forceRefresh)
            return false;

        var point = await geocoding.GeocodeAsync(address, ct, forceRefresh, hints);
        if (!point.HasValue)
            return false;

        apply(point.Value.Latitude, point.Value.Longitude);
        return true;
    }
}
