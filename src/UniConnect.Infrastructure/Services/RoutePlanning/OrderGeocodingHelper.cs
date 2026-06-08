using Microsoft.EntityFrameworkCore;

using UniConnect.Delivery.DTOs;

using UniConnect.Delivery.Entities;

using UniConnect.Infrastructure.Data;

using UniConnect.RoutePlanning.Interfaces;

using UniConnect.RoutePlanning.Models;

using UniConnect.RoutePlanning.Routing;

using UniConnect.Tenant.Interfaces;



namespace UniConnect.Infrastructure.Services.RoutePlanning;



public sealed class OrderGeocodingHelper(

    AppDbContext db,

    IGeocodingService geocoding,

    ITenantGeocodingConfigProvider geocodingConfig)

{

    public async Task EnsureOrderCoordinatesAsync(

        DeliveryOrder order,

        CancellationToken ct = default,

        bool upgradeDeterministic = false)

    {

        var tenantId = order.TenantId;

        var pickupHints = AddressGeocodeHints.Parse(order.PickupAddress);

        var deliveryHints = AddressGeocodeHints.Parse(order.DeliveryAddress);

        var sharedHints = AddressGeocodeHints.Merge(pickupHints, deliveryHints);



        var changed = false;

        changed |= await EnsureEndpointAsync(

            order.PickupAddress,

            AddressGeocodeHints.Merge(sharedHints, pickupHints),

            order.PickupLatitude,

            order.PickupLongitude,

            order.PickupFormattedAddress,

            (lat, lng, formatted) =>

            {

                order.PickupLatitude = lat;

                order.PickupLongitude = lng;

                order.PickupFormattedAddress = formatted;

            },

            upgradeDeterministic,

            tenantId,

            ct);

        changed |= await EnsureEndpointAsync(

            order.DeliveryAddress,

            AddressGeocodeHints.Merge(sharedHints, deliveryHints),

            order.DeliveryLatitude,

            order.DeliveryLongitude,

            order.DeliveryFormattedAddress,

            (lat, lng, formatted) =>

            {

                order.DeliveryLatitude = lat;

                order.DeliveryLongitude = lng;

                order.DeliveryFormattedAddress = formatted;

            },

            upgradeDeterministic,

            tenantId,

            ct);



        if (changed)

            await db.SaveChangesAsync(ct);

    }



    public async Task<bool> HasStaleOrderGeocodeAsync(DeliveryOrder order, CancellationToken ct = default)
    {
        var tenantId = order.TenantId;
        if (order.PickupLatitude.HasValue && order.PickupLongitude.HasValue
            && await CoordinatesOutOfSyncWithCacheAsync(
                order.PickupAddress, order.PickupLatitude, order.PickupLongitude, tenantId, ct))
            return true;

        return order.DeliveryLatitude.HasValue && order.DeliveryLongitude.HasValue
            && await CoordinatesOutOfSyncWithCacheAsync(
                order.DeliveryAddress, order.DeliveryLatitude, order.DeliveryLongitude, tenantId, ct);
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



    public async Task<bool> HasLowQualityGeocodeAsync(

        string address,

        Guid tenantId,

        CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(address))

            return true;



        var source = await GetCachedSourceAsync(address, tenantId, ct);

        if (source is null)

            return true;



        return await geocoding.ShouldUpgradeCachedSourceAsync(source, address, tenantId, ct);

    }



    public async Task<bool> CoordinatesOutOfSyncWithCacheAsync(

        string address,

        decimal? latitude,

        decimal? longitude,

        Guid tenantId,

        CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(address) || latitude is null || longitude is null)

            return false;



        var cacheKey = await geocodingConfig.ResolveCacheKeyAsync(address, tenantId, ct);

        var cached = await db.GeocodedAddresses.AsNoTracking()

            .Where(g => g.NormalizedAddress == cacheKey)

            .Select(g => new { g.Latitude, g.Longitude })

            .FirstOrDefaultAsync(ct);



        if (cached is null)

            return true;



        const decimal tolerance = 0.0005m;

        return Math.Abs(cached.Latitude - latitude.Value) > tolerance

            || Math.Abs(cached.Longitude - longitude.Value) > tolerance;

    }



    public async Task<bool> HasDeterministicGeocodeAsync(string address, Guid tenantId, CancellationToken ct = default)

    {

        var source = await GetCachedSourceAsync(address, tenantId, ct);

        return source?.Equals("deterministic", StringComparison.OrdinalIgnoreCase) == true;

    }



    public async Task<string?> GetCachedSourceAsync(string address, Guid tenantId, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(address))

            return null;



        var cacheKey = await geocodingConfig.ResolveCacheKeyAsync(address, tenantId, ct);

        return await db.GeocodedAddresses.AsNoTracking()

            .Where(g => g.NormalizedAddress == cacheKey)

            .Select(g => g.Source)

            .FirstOrDefaultAsync(ct);

    }



    public async Task<IReadOnlyDictionary<string, string>> GetCachedSourcesAsync(

        IEnumerable<string> addresses,

        Guid tenantId,

        CancellationToken ct = default)

    {

        var keys = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var address in addresses.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase))

        {

            var cacheKey = await geocodingConfig.ResolveCacheKeyAsync(address, tenantId, ct);

            keys[address] = cacheKey;

        }



        if (keys.Count == 0)

            return new Dictionary<string, string>(StringComparer.Ordinal);



        var cacheKeyList = keys.Values.Distinct(StringComparer.Ordinal).ToList();

        var rows = await db.GeocodedAddresses.AsNoTracking()

            .Where(g => cacheKeyList.Contains(g.NormalizedAddress))

            .ToListAsync(ct);



        var byCacheKey = rows.ToDictionary(r => r.NormalizedAddress, r => r.Source, StringComparer.Ordinal);

        return keys
            .Where(kvp => byCacheKey.ContainsKey(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => byCacheKey[kvp.Value], StringComparer.OrdinalIgnoreCase);
    }



    public string? ResolveCachedSource(string address, IReadOnlyDictionary<string, string> sourcesByAddress) =>

        string.IsNullOrWhiteSpace(address) ? null : sourcesByAddress.GetValueOrDefault(address);



    public Task<GeocodeResult?> GeocodeAddressAsync(

        string address,

        Guid tenantId,

        CancellationToken ct = default,

        AddressGeocodeHints hints = default) =>

        geocoding.GeocodeAsync(address, ct, hints: hints, tenantId: tenantId);



    public async Task<CheckAddressesResultDto> CheckAddressesAsync(

        Guid tenantId,

        string pickupAddress,

        string deliveryAddress,

        CancellationToken ct = default)

    {

        pickupAddress = pickupAddress.Trim();

        deliveryAddress = deliveryAddress.Trim();



        var pickupHints = AddressGeocodeHints.Parse(pickupAddress);

        var deliveryHints = AddressGeocodeHints.Parse(deliveryAddress);

        var sharedHints = AddressGeocodeHints.Merge(pickupHints, deliveryHints);



        var pickup = await CheckAddressAsync(

            pickupAddress,

            AddressGeocodeHints.Merge(sharedHints, pickupHints),

            tenantId,

            ct);

        var delivery = await CheckAddressAsync(

            deliveryAddress,

            AddressGeocodeHints.Merge(sharedHints, deliveryHints),

            tenantId,

            ct);



        return new CheckAddressesResultDto(pickup, delivery);

    }



    private async Task<AddressCheckResultDto> CheckAddressAsync(

        string address,

        AddressGeocodeHints hints,

        Guid tenantId,

        CancellationToken ct)

    {

        if (string.IsNullOrWhiteSpace(address))

        {

            return new AddressCheckResultDto(

                string.Empty,

                string.Empty,

                null,

                null,

                null,

                null,

                false,

                "Address is required.");

        }



        var geocoded = await geocoding.GeocodeAsync(address, ct, forceRefresh: true, hints, tenantId);

        var cached = await GetCachedGeocodeAsync(address, tenantId, ct);

        var source = cached?.Source ?? await GetCachedSourceAsync(address, tenantId, ct);



        if (!geocoded.HasValue && cached is null)

        {

            var failureMessage = geocoding.GetLastErrorMessage()
                ?? "Could not locate this address. Try a full street address with city and ZIP.";

            return new AddressCheckResultDto(

                address,

                address,

                null,

                null,

                null,

                source,

                false,

                failureMessage);

        }



        var formatted = cached?.FormattedAddress?.Trim() ?? geocoded?.FormattedAddress?.Trim();

        var standardized = string.IsNullOrWhiteSpace(formatted) ? null : formatted;

        var suggested = standardized ?? address;

        var latitude = cached?.Latitude ?? geocoded?.Latitude;

        var longitude = cached?.Longitude ?? geocoded?.Longitude;

        var message = DescribeCheckResult(source, standardized, standardized is not null && !address.Equals(standardized, StringComparison.OrdinalIgnoreCase));



        return new AddressCheckResultDto(

            address,

            suggested,

            standardized,

            latitude,

            longitude,

            source,

            geocoded.HasValue || cached is not null,

            message);

    }



    private async Task<(decimal Latitude, decimal Longitude, string Source, string? FormattedAddress)?> GetCachedGeocodeAsync(

        string address,

        Guid tenantId,

        CancellationToken ct)

    {

        var cacheKey = await geocodingConfig.ResolveCacheKeyAsync(address, tenantId, ct);

        var row = await db.GeocodedAddresses.AsNoTracking()

            .Where(g => g.NormalizedAddress == cacheKey)

            .Select(g => new { g.Latitude, g.Longitude, g.Source, g.FormattedAddress })

            .FirstOrDefaultAsync(ct);



        return row is null ? null : (row.Latitude, row.Longitude, row.Source, row.FormattedAddress);

    }



    private static string DescribeCheckResult(string? source, string? formatted, bool addressUpdated)

    {

        var normalized = source?.ToLowerInvariant();

        if (normalized is "deterministic")

            return "Approximate location only. Enter a full street address with city and ZIP, then check again.";

        if (normalized is "postal")

            return "Matched to a postal code area. Add a street address for accurate routing.";

        if (addressUpdated && !string.IsNullOrWhiteSpace(formatted))

            return "Standardized address applied from geocoder.";

        if (normalized is "census" or "nominatim" or "google")
            return "Street-level match found.";

        return "Location found.";

    }



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

        string? formattedAddress,

        Action<decimal, decimal, string?> apply,

        bool upgradeDeterministic,

        Guid tenantId,

        CancellationToken ct)

    {

        var missing = !latitude.HasValue || !longitude.HasValue;

        var lowQuality = await HasLowQualityGeocodeAsync(address, tenantId, ct);

        var outOfSync = !missing

            && await CoordinatesOutOfSyncWithCacheAsync(address, latitude, longitude, tenantId, ct);

        var missingFormatted = string.IsNullOrWhiteSpace(formattedAddress)

            && !string.IsNullOrWhiteSpace(address);

        var forceRefresh = (missing && upgradeDeterministic)

            || outOfSync

            || (missingFormatted && !missing)

            || (!missing && upgradeDeterministic && lowQuality);

        if (!missing && !forceRefresh)

            return false;



        var result = await geocoding.GeocodeAsync(address, ct, forceRefresh, hints, tenantId);

        if (!result.HasValue)

            return false;



        apply(result.Value.Latitude, result.Value.Longitude, result.Value.FormattedAddress);

        return true;

    }

}

