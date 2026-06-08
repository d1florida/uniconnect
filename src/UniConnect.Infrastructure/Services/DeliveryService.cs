using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniConnect.Application;
using UniConnect.Application.Interfaces;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Entities;
using UniConnect.Delivery.Enums;
using UniConnect.Delivery.Interfaces;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;
using UniConnect.Insights.Enums;
using UniConnect.Insights.Interfaces;
using UniConnect.Tenant;
using UniConnect.Tenant.Enums;
using UniConnect.GeneralFleet.DTOs;
using UniConnect.GeneralFleet.Enums;
using UniConnect.GeneralFleet.Entities;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Services.RoutePlanning;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.RoutePlanning.Options;
using UniConnect.RoutePlanning.Routing;
using UniConnect.RoutePlanning.Enums;
using UniConnect.RoboTaxi.Entities;
using UniConnect.RoboTaxi.Enums;

namespace UniConnect.Infrastructure.Services;

public class DeliveryService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IOperationalEventRecorder events,
    ICustomerDirectory customers,
    IFixedRouteDirectory fixedRoutes,
    OrderGeocodingHelper orderGeocoding,
    IDepotDirectory depots,
    ITravelTimeMatrix travelTimeMatrix,
    IOptions<RoutePlanningOptions> planningOptions) : IDeliveryService
{
    private static readonly DeliveryOrderStatus[] ActiveOrderStatuses =
    [
        DeliveryOrderStatus.Assigned,
        DeliveryOrderStatus.PickedUp,
        DeliveryOrderStatus.InTransit
    ];

    public async Task<DeliveryDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var tenantIds = await ScopedDeliveryTenantIdsAsync(ct);
        var orders = await ScopedOrders()
            .Where(o => tenantIds.Contains(o.TenantId))
            .ToListAsync(ct);

        var assignments = await db.DeliveryAssignments.AsNoTracking()
            .Where(a => orders.Select(o => o.Id).Contains(a.DeliveryOrderId))
            .ToListAsync(ct);

        var activeAssignmentOrderIds = orders
            .Where(o => ActiveOrderStatuses.Contains(o.Status))
            .Select(o => o.Id)
            .ToHashSet();

        var routes = await ScopedRoutes()
            .Where(r => tenantIds.Contains(r.TenantId))
            .ToListAsync(ct);

        return new DeliveryDashboardDto(
            orders.Count(o => o.Status == DeliveryOrderStatus.Created && !o.FixedRouteTemplateId.HasValue),
            orders.Count(o => o.Status == DeliveryOrderStatus.InTransit),
            orders.Count,
            assignments.Count(a => activeAssignmentOrderIds.Contains(a.DeliveryOrderId) && a.AutomationMode == AutomationMode.Autonomous),
            assignments.Count(a => activeAssignmentOrderIds.Contains(a.DeliveryOrderId) && a.AutomationMode == AutomationMode.Conventional),
            routes.Count(r => r.Status == DeliveryRouteStatus.InProgress),
            routes.Count(r => r.Status == DeliveryRouteStatus.Planned),
            orders.Count(o => o.Status == DeliveryOrderStatus.Created && !o.FixedRouteTemplateId.HasValue),
            orders.Count(o => o.Status == DeliveryOrderStatus.Created && o.FixedRouteTemplateId.HasValue),
            routes.Count(r => r.Status == DeliveryRouteStatus.Draft),
            routes.Count(r =>
                (r.Status == DeliveryRouteStatus.Draft || r.Status == DeliveryRouteStatus.Planned)
                && !r.DriverId.HasValue
                && r.AutomationMode != AutomationMode.Autonomous));
    }

    public async Task<IReadOnlyList<DeliveryOrderDto>> GetOrdersAsync(Guid tenantId, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        currentUser.EnsureTenantAccess(tenantId);
        var orders = await ScopedOrders()
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .Include(o => o.Assignment)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        var vehicleIds = orders.Where(o => o.Assignment != null).Select(o => o.Assignment!.VehicleId).Distinct().ToList();
        var driverIds = orders.Where(o => o.Assignment?.DriverId != null).Select(o => o.Assignment!.DriverId!.Value).Distinct().ToList();
        var vehicleInfo = await VehicleDisplayInfoAsync(vehicleIds, ct);
        var driverNames = await DriverNamesAsync(driverIds, ct);
        var geocodeSources = await orderGeocoding.GetCachedSourcesAsync(
            orders.SelectMany(o => new[] { o.PickupAddress, o.DeliveryAddress }),
            tenantId,
            ct);
        var templateNames = await FixedRouteTemplateNamesAsync(
            orders.Where(o => o.FixedRouteTemplateId.HasValue).Select(o => o.FixedRouteTemplateId!.Value).Distinct(),
            ct);

        return orders.Select(o =>
        {
            VehicleDisplayInfo? info = null;
            if (o.Assignment != null && vehicleInfo.TryGetValue(o.Assignment.VehicleId, out var vi))
                info = vi;
            return MapOrder(
                o,
                info?.VehicleNumber,
                info?.LicensePlate,
                o.Assignment?.DriverId is Guid did ? driverNames.GetValueOrDefault(did) : null,
                geocodeSources,
                o.FixedRouteTemplateId is Guid tid ? templateNames.GetValueOrDefault(tid) : null);
        }).ToList();
    }

    public async Task<DeliveryOrderDto?> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var order = await ScopedOrders()
            .AsNoTracking()
            .Include(o => o.Assignment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return null;

        if (NeedsGeocode(order) || await orderGeocoding.HasStaleOrderGeocodeAsync(order, ct))
        {
            await BackfillMissingCoordinatesOnlyAsync([order.Id], ct);
            order = await ScopedOrders()
                .AsNoTracking()
                .Include(o => o.Assignment)
                .FirstAsync(o => o.Id == orderId, ct);
        }

        string? vehicleNumber = null;
        string? licensePlate = null;
        string? driverName = null;
        if (order.Assignment != null)
        {
            var info = await GetVehicleDisplayInfoAsync(order.Assignment.VehicleId, ct);
            vehicleNumber = info?.VehicleNumber;
            licensePlate = info?.LicensePlate;
            if (order.Assignment.DriverId.HasValue)
                driverName = await db.Drivers.AsNoTracking()
                    .Where(d => d.Id == order.Assignment.DriverId.Value)
                    .Select(d => d.DisplayName)
                    .FirstOrDefaultAsync(ct);
        }

        var geocodeSources = await orderGeocoding.GetCachedSourcesAsync(
            [order.PickupAddress, order.DeliveryAddress],
            order.TenantId,
            ct);
        string? templateName = null;
        if (order.FixedRouteTemplateId.HasValue)
        {
            var names = await FixedRouteTemplateNamesAsync([order.FixedRouteTemplateId.Value], ct);
            templateName = names.GetValueOrDefault(order.FixedRouteTemplateId.Value);
        }

        return MapOrder(order, vehicleNumber, licensePlate, driverName, geocodeSources, templateName);
    }

    public async Task<DeliveryOrderDto> CreateOrderAsync(Guid tenantId, CreateDeliveryOrderRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        currentUser.EnsureTenantAccess(tenantId);

        Customer customer;
        if (request.CustomerId.HasValue)
        {
            customer = await db.Customers.FirstOrDefaultAsync(c =>
                c.Id == request.CustomerId.Value && c.TenantId == tenantId && c.IsActive, ct)
                ?? throw new ArgumentException("Customer not found.");
        }
        else
        {
            customer = await customers.GetOrCreateAsync(
                tenantId,
                request.RecipientName,
                request.RecipientPhone,
                request.DeliveryAddress,
                ct);
        }
        var externalRef = ResolveExternalRef(request.ExternalRef, customer.ExternalRef);
        var order = new DeliveryOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Status = DeliveryOrderStatus.Created,
            PickupAddress = request.PickupAddress,
            DeliveryAddress = request.DeliveryAddress,
            RecipientName = request.RecipientName,
            RecipientPhone = request.RecipientPhone,
            ParcelDescription = request.ParcelDescription,
            ExternalRef = externalRef,
            CustomerId = customer.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.DeliveryOrders.Add(order);
        await db.SaveChangesAsync(ct);
        await customers.SyncDeliveryAddressAsync(tenantId, customer.Id, request.DeliveryAddress, ct);
        if (externalRef is not null)
            await customers.SyncExternalRefAsync(tenantId, customer.Id, externalRef, ct);
        await orderGeocoding.EnsureOrderCoordinatesAsync(order, ct);
        await fixedRoutes.ApplyHoldToOrderAsync(order, customer.Id, ct);
        await db.SaveChangesAsync(ct);

        await RecordDeliveryEventAsync(
            tenantId,
            DeliveryEventTypes.OrderCreated,
            orderId: order.Id,
            routeId: null,
            stopId: null,
            vehicleId: null,
            driverId: null,
            customerId: customer.Id,
            customerLabel: customer.Name,
            narrative: $"Order created for {customer.Name} delivering to {order.DeliveryAddress}.",
            context: new Dictionary<string, object?> { ["status"] = order.Status.ToString() },
            ct: ct);

        return await GetOrderAsync(order.Id, ct) ?? MapOrder(order, null, null, null);
    }

    public async Task<DeliveryOrderDto> UpdateOrderAsync(Guid orderId, UpdateDeliveryOrderRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var order = await ScopedOrders()
            .Include(o => o.Assignment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException("Order not found.");

        if (!CanEditOrder(order.Status))
            throw new InvalidOperationException("This order can no longer be edited.");

        var pickupAddress = (request.PickupAddress ?? string.Empty).Trim();
        var deliveryAddress = (request.DeliveryAddress ?? string.Empty).Trim();
        var recipientName = (request.RecipientName ?? string.Empty).Trim();
        var recipientPhone = (request.RecipientPhone ?? string.Empty).Trim();
        var parcelDescription = (request.ParcelDescription ?? string.Empty).Trim();
        var externalRef = string.IsNullOrWhiteSpace(request.ExternalRef) ? null : request.ExternalRef.Trim();

        if (string.IsNullOrWhiteSpace(deliveryAddress))
            throw new ArgumentException("Delivery address is required.");
        if (string.IsNullOrWhiteSpace(recipientName))
            throw new ArgumentException("Recipient name is required.");

        var pickupChanged = !AddressesEqual(order.PickupAddress, pickupAddress);
        var deliveryChanged = !AddressesEqual(order.DeliveryAddress, deliveryAddress);
        var recipientChanged = !string.Equals(order.RecipientName, recipientName, StringComparison.Ordinal)
            || !string.Equals(order.RecipientPhone, recipientPhone, StringComparison.Ordinal);
        var parcelChanged = !string.Equals(order.ParcelDescription, parcelDescription, StringComparison.Ordinal);
        var externalRefChanged = !string.Equals(order.ExternalRef, externalRef, StringComparison.Ordinal);

        order.PickupAddress = pickupAddress;
        order.DeliveryAddress = deliveryAddress;
        order.RecipientName = recipientName;
        order.RecipientPhone = recipientPhone;
        order.ParcelDescription = parcelDescription;
        order.ExternalRef = externalRef;

        var customer = await customers.GetOrCreateAsync(order.TenantId, recipientName, recipientPhone, deliveryAddress, ct);
        order.CustomerId = customer.Id;

        if (deliveryChanged)
            await customers.SyncDeliveryAddressAsync(order.TenantId, customer.Id, deliveryAddress, ct);

        if (externalRefChanged)
            await customers.SyncExternalRefAsync(order.TenantId, customer.Id, externalRef, ct);

        if (pickupChanged)
        {
            order.PickupLatitude = null;
            order.PickupLongitude = null;
            order.PickupFormattedAddress = null;
        }

        if (deliveryChanged)
        {
            order.DeliveryLatitude = null;
            order.DeliveryLongitude = null;
            order.DeliveryFormattedAddress = null;
        }

        if (pickupChanged || deliveryChanged || recipientChanged || parcelChanged || externalRefChanged)
        {
            var routeStops = await db.DeliveryRouteStops
                .Where(s => s.DeliveryOrderId == order.Id)
                .ToListAsync(ct);
            foreach (var stop in routeStops)
            {
                if (stop.StopType == DeliveryStopType.Pickup && pickupChanged)
                    stop.Address = pickupAddress;
                if (stop.StopType == DeliveryStopType.Dropoff && deliveryChanged)
                    stop.Address = deliveryAddress;
                if (recipientChanged)
                {
                    stop.RecipientName = recipientName;
                    stop.RecipientPhone = string.IsNullOrWhiteSpace(recipientPhone) ? null : recipientPhone;
                }
                if (parcelChanged)
                    stop.ParcelDescription = string.IsNullOrWhiteSpace(parcelDescription) ? null : parcelDescription;
            }
        }

        await db.SaveChangesAsync(ct);

        if (pickupChanged || deliveryChanged)
            await orderGeocoding.EnsureOrderCoordinatesAsync(order, ct, upgradeDeterministic: true);

        try
        {
            if (pickupChanged || deliveryChanged || recipientChanged)
                await SyncOrderIntoPendingPlanProposalsAsync(order, ct);

            await RecordOrderEventAsync(
                order,
                DeliveryEventTypes.OrderStatusChanged,
                $"Order updated for {order.RecipientName} delivering to {order.DeliveryAddress}.",
                ct);
        }
        catch
        {
            // Insights sync / event logging must not block order updates.
        }

        return (await GetOrderAsync(orderId, ct))!;
    }

    private async Task SyncOrderIntoPendingPlanProposalsAsync(DeliveryOrder order, CancellationToken ct)
    {
        var runs = await db.RoutePlanRuns
            .Where(p => p.TenantId == order.TenantId && p.Status == RoutePlanRunStatus.Completed)
            .ToListAsync(ct);

        var orderLookup = new Dictionary<Guid, OrderStopSnapshot>
        {
            [order.Id] = new OrderStopSnapshot(
                order.Id,
                order.PickupAddress,
                order.DeliveryAddress,
                order.RecipientName,
                order.ParcelDescription,
                order.PickupLatitude,
                order.PickupLongitude,
                order.DeliveryLatitude,
                order.DeliveryLongitude),
        };
        var changed = false;

        foreach (var run in runs)
        {
            var proposals = PlanProposalSync.Deserialize(run.ProposalJson);
            if (!proposals.Any(p => p.Stops.Any(s => s.OrderId == order.Id)))
                continue;

            run.ProposalJson = PlanProposalSync.Serialize(PlanProposalSync.RefreshFromOrders(proposals, orderLookup));
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    public async Task<DeliveryOrderDto> UpdateStatusAsync(Guid orderId, UpdateDeliveryStatusRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var order = await ScopedOrders()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException("Order not found.");

        var previous = order.Status;
        order.Status = request.Status;
        if (request.Status == DeliveryOrderStatus.Delivered)
            order.DeliveredAt = DateTime.UtcNow;
        if (request.Status == DeliveryOrderStatus.Failed)
        {
            order.FailedAt = DateTime.UtcNow;
            order.FailureReason ??= "Marked failed";
        }

        if (order.Assignment is not null && request.Status is DeliveryOrderStatus.Delivered or DeliveryOrderStatus.Failed or DeliveryOrderStatus.Cancelled)
            order.Assignment.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        var eventType = request.Status switch
        {
            DeliveryOrderStatus.Delivered => DeliveryEventTypes.OrderDelivered,
            DeliveryOrderStatus.Failed => DeliveryEventTypes.OrderFailed,
            _ => DeliveryEventTypes.OrderStatusChanged
        };

        await RecordOrderEventAsync(order, eventType, $"Order status changed from {previous} to {order.Status}.", ct);
        return (await GetOrderAsync(orderId, ct))!;
    }

    public async Task<DeliveryOrderDto> AssignAsync(Guid orderId, AssignDeliveryRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var order = await ScopedOrders()
            .Include(o => o.Assignment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException("Order not found.");

        await ValidateVehicleForAssignmentAsync(order.TenantId, request.VehicleId, request.AutomationMode, ct);
        var driverId = await ResolveDriverIdForAssignmentAsync(order.TenantId, request.AutomationMode, request.DriverId, ct);

        if (order.Assignment is null)
        {
            order.Assignment = new DeliveryAssignment
            {
                Id = Guid.NewGuid(),
                DeliveryOrderId = order.Id,
                VehicleId = request.VehicleId,
                DriverId = driverId,
                AutomationMode = request.AutomationMode,
                AssignedAt = DateTime.UtcNow
            };
            db.DeliveryAssignments.Add(order.Assignment);
        }
        else
        {
            order.Assignment.VehicleId = request.VehicleId;
            order.Assignment.DriverId = driverId;
            order.Assignment.AutomationMode = request.AutomationMode;
            order.Assignment.AssignedAt = DateTime.UtcNow;
        }

        if (order.Status == DeliveryOrderStatus.Created)
            order.Status = DeliveryOrderStatus.Assigned;

        await db.SaveChangesAsync(ct);

        var vehicleNumber = await db.Vehicles.AsNoTracking().Where(v => v.Id == request.VehicleId).Select(v => v.VehicleNumber).FirstAsync(ct);
        string? driverLabel = null;
        if (driverId.HasValue)
            driverLabel = await db.Drivers.AsNoTracking().Where(d => d.Id == driverId).Select(d => d.DisplayName).FirstOrDefaultAsync(ct);

        await RecordDeliveryEventAsync(
            order.TenantId,
            DeliveryEventTypes.OrderAssigned,
            orderId: order.Id,
            routeId: null,
            stopId: null,
            vehicleId: request.VehicleId,
            driverId: driverId,
            customerId: order.CustomerId,
            vehicleLabel: vehicleNumber,
            driverLabel: driverLabel,
            customerLabel: order.RecipientName,
            narrative: driverLabel is not null
                ? $"Order assigned to {driverLabel} on vehicle {vehicleNumber}."
                : $"Order assigned to vehicle {vehicleNumber}.",
            metrics: new Dictionary<string, object?> { ["automationMode"] = request.AutomationMode.ToString() },
            ct: ct);

        return (await GetOrderAsync(orderId, ct))!;
    }

    public async Task<IReadOnlyList<DeliveryVehicleDto>> GetVehiclesAsync(Guid tenantId, Guid? depotId = null, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        currentUser.EnsureTenantAccess(tenantId);
        var query = db.Vehicles.AsNoTracking().Where(v => v.TenantId == tenantId);
        if (depotId.HasValue)
            query = query.Where(v => v.HomeDepotId == depotId.Value);

        var vehicles = await query.ToListAsync(ct);
        var profiles = await db.RoboTaxiProfiles.AsNoTracking()
            .Where(p => vehicles.Select(v => v.Id).Contains(p.VehicleId))
            .ToDictionaryAsync(p => p.VehicleId, ct);
        var locs = await ResolveVehicleLocationsAsync(vehicles, ct);
        var depotNames = await ResolveDepotNamesAsync(vehicles, ct);

        return vehicles.Select(v =>
        {
            profiles.TryGetValue(v.Id, out var profile);
            depotNames.TryGetValue(v.HomeDepotId ?? Guid.Empty, out var depotName);
            return new DeliveryVehicleDto(
                v.Id, v.Category, v.VehicleNumber, v.LicensePlate, v.Make, v.Model,
                profile != null, v.Status, profile?.OperationalState, locs.GetValueOrDefault(v.Id),
                v.HomeDepotId, depotName);
        }).ToList();
    }

    public async Task<DeliveryVehicleDto> AssignVehicleHomeDepotAsync(
        Guid tenantId,
        Guid vehicleId,
        AssignVehicleHomeDepotRequest request,
        CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        currentUser.EnsureTenantAccess(tenantId);
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Vehicle not found.");

        if (request.HomeDepotId.HasValue)
        {
            var depotExists = await db.Depots.AsNoTracking()
                .AnyAsync(d => d.Id == request.HomeDepotId.Value && d.TenantId == tenantId && d.IsActive, ct);
            if (!depotExists)
                throw new ArgumentException("Depot not found.");
        }

        vehicle.HomeDepotId = request.HomeDepotId;
        await db.SaveChangesAsync(ct);

        if (request.HomeDepotId.HasValue)
            await RecordVehicleAtHomeDepotAsync(vehicleId, request.HomeDepotId.Value, ct);

        var vehicles = await GetVehiclesAsync(tenantId, ct: ct);
        return vehicles.First(v => v.Id == vehicleId);
    }

    private async Task RecordVehicleAtHomeDepotAsync(Guid vehicleId, Guid depotId, CancellationToken ct)
    {
        var depot = await db.Depots.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == depotId, ct);
        if (depot?.Latitude is null || depot.Longitude is null)
            return;

        db.VehicleLocations.Add(new VehicleLocation
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            Latitude = depot.Latitude.Value,
            Longitude = depot.Longitude.Value,
            RecordedAt = DateTime.UtcNow,
            SpeedKph = 0,
            Source = LocationSource.Manual
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<Guid, LocationDto>> ResolveVehicleLocationsAsync(
        IReadOnlyList<Vehicle> vehicles,
        CancellationToken ct)
    {
        var gpsLocs = await LocationHelper.GetLatestForVehiclesAsync(db, vehicles.Select(v => v.Id), ct);
        if (vehicles.Count == 0)
            return gpsLocs;

        var depotIds = vehicles.Where(v => v.HomeDepotId.HasValue).Select(v => v.HomeDepotId!.Value).Distinct().ToList();
        if (depotIds.Count == 0)
            return gpsLocs;

        var depots = await db.Depots.AsNoTracking()
            .Where(d => depotIds.Contains(d.Id) && d.Latitude != null && d.Longitude != null)
            .ToDictionaryAsync(d => d.Id, ct);

        var vehicleIds = vehicles.Select(v => v.Id).ToList();
        var activeVehicleIds = (await db.DeliveryAssignments.AsNoTracking()
            .Where(a => vehicleIds.Contains(a.VehicleId))
            .Join(
                db.DeliveryOrders.AsNoTracking(),
                a => a.DeliveryOrderId,
                o => o.Id,
                (a, o) => new { a.VehicleId, o.Status })
            .Where(x => ActiveOrderStatuses.Contains(x.Status))
            .Select(x => x.VehicleId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        var result = new Dictionary<Guid, LocationDto>();
        foreach (var vehicle in vehicles)
        {
            if (activeVehicleIds.Contains(vehicle.Id) && gpsLocs.TryGetValue(vehicle.Id, out var activeGps))
            {
                result[vehicle.Id] = activeGps;
                continue;
            }

            if (vehicle.HomeDepotId.HasValue
                && depots.TryGetValue(vehicle.HomeDepotId.Value, out var depot)
                && depot.Latitude.HasValue && depot.Longitude.HasValue)
            {
                gpsLocs.TryGetValue(vehicle.Id, out var existing);
                result[vehicle.Id] = new LocationDto(
                    depot.Latitude.Value,
                    depot.Longitude.Value,
                    existing?.RecordedAt ?? DateTime.UtcNow,
                    0);
                continue;
            }

            if (gpsLocs.TryGetValue(vehicle.Id, out var gps))
                result[vehicle.Id] = gps;
        }

        return result;
    }

    private async Task<Dictionary<Guid, string>> ResolveDepotNamesAsync(IReadOnlyList<Vehicle> vehicles, CancellationToken ct)
    {
        var depotIds = vehicles.Where(v => v.HomeDepotId.HasValue).Select(v => v.HomeDepotId!.Value).Distinct().ToList();
        if (depotIds.Count == 0)
            return [];

        return await db.Depots.AsNoTracking()
            .Where(d => depotIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
    }

    public async Task<IReadOnlyList<DeliveryTrackingDto>> GetTrackingAsync(Guid tenantId, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        currentUser.EnsureTenantAccess(tenantId);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.TenantId == tenantId).ToListAsync(ct);
        var profiles = await db.RoboTaxiProfiles.AsNoTracking()
            .Where(p => vehicles.Select(v => v.Id).Contains(p.VehicleId))
            .ToDictionaryAsync(p => p.VehicleId, ct);
        var locs = await ResolveVehicleLocationsAsync(vehicles, ct);

        var activeOrders = await db.DeliveryAssignments.AsNoTracking()
            .Where(a => vehicles.Select(v => v.Id).Contains(a.VehicleId))
            .Join(db.DeliveryOrders.AsNoTracking(),
                a => a.DeliveryOrderId,
                o => o.Id,
                (a, o) => new { a.VehicleId, Order = o })
            .Where(x => ActiveOrderStatuses.Contains(x.Order.Status))
            .ToListAsync(ct);

        var activeByVehicle = activeOrders
            .GroupBy(x => x.VehicleId)
            .ToDictionary(g => g.Key, g => g.First().Order);

        return vehicles.Select(v =>
        {
            profiles.TryGetValue(v.Id, out var profile);
            activeByVehicle.TryGetValue(v.Id, out var active);
            return new DeliveryTrackingDto(
                v.Id, v.Category, v.VehicleNumber, v.LicensePlate, profile != null, profile?.OperationalState, locs.GetValueOrDefault(v.Id),
                active?.Id, active?.Status, active?.DeliveryAddress);
        }).ToList();
    }

    public async Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesAsync(Guid tenantId, CancellationToken ct = default)
    {
        currentUser.EnsureTenantAccess(tenantId);
        var routes = await ScopedRoutes()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .Include(r => r.Stops)
            .OrderByDescending(r => r.ScheduledDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var vehicleIds = routes.Where(r => r.VehicleId.HasValue).Select(r => r.VehicleId!.Value).Distinct().ToList();
        var driverIds = routes.Where(r => r.DriverId.HasValue).Select(r => r.DriverId!.Value).Distinct().ToList();
        var vehicleInfo = await VehicleDisplayInfoAsync(vehicleIds, ct);
        var driverNames = await DriverNamesAsync(driverIds, ct);

        return routes.Select(r =>
        {
            VehicleDisplayInfo? info = null;
            if (r.VehicleId.HasValue && vehicleInfo.TryGetValue(r.VehicleId.Value, out var vi))
                info = vi;
            return MapRouteSummary(
                r,
                info?.VehicleNumber,
                info?.LicensePlate,
                r.DriverId is Guid did ? driverNames.GetValueOrDefault(did) : null);
        }).ToList();
    }

    public async Task<DeliveryRouteDetailDto?> GetRouteAsync(Guid routeId, CancellationToken ct = default)
    {
        var route = await ScopedRoutes()
            .AsNoTracking()
            .Include(r => r.Stops.OrderBy(s => s.Sequence))
            .FirstOrDefaultAsync(r => r.Id == routeId, ct);
        if (route is null) return null;

        string? vehicleNumber = null;
        string? licensePlate = null;
        string? driverName = null;
        if (route.VehicleId.HasValue)
        {
            var info = await GetVehicleDisplayInfoAsync(route.VehicleId.Value, ct);
            vehicleNumber = info?.VehicleNumber;
            licensePlate = info?.LicensePlate;
        }
        if (route.DriverId.HasValue)
            driverName = await db.Drivers.AsNoTracking()
                .Where(d => d.Id == route.DriverId.Value)
                .Select(d => d.DisplayName)
                .FirstOrDefaultAsync(ct);

        return await MapRouteDetailAsync(route, vehicleNumber, licensePlate, driverName, ct);
    }

    public async Task<DeliveryRouteDetailDto> CreateRouteAsync(Guid tenantId, CreateDeliveryRouteRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        currentUser.EnsureTenantAccess(tenantId);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Route name is required.");
        if (request.Stops.Count == 0)
            throw new ArgumentException("At least one stop is required.");

        var depotAddress = await depots.ResolveDepotAddressAsync(tenantId, request.DepotId, request.DepotAddress, ct);
        GeoPoint? depotPoint = (await orderGeocoding.GeocodeAddressAsync(depotAddress, tenantId, ct))?.ToPoint();

        var routeId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var route = new DeliveryRoute
        {
            Id = routeId,
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Status = DeliveryRouteStatus.Draft,
            DepotAddress = depotAddress,
            DepotId = request.DepotId,
            ScheduledDate = request.ScheduledDate,
            CreatedAt = now,
            Stops =
            [
                new DeliveryRouteStop
                {
                    Id = Guid.NewGuid(),
                    RouteId = routeId,
                    Sequence = 0,
                    StopType = DeliveryStopType.Depot,
                    Status = DeliveryStopStatus.Pending,
                    Address = depotAddress
                }
            ]
        };

        var seq = 1;
        var linkedOrderIds = request.Stops
            .Where(s => s.DeliveryOrderId.HasValue)
            .Select(s => s.DeliveryOrderId!.Value)
            .Distinct()
            .ToList();
        var linkedOrders = linkedOrderIds.Count == 0
            ? new Dictionary<Guid, DeliveryOrder>()
            : await db.DeliveryOrders.AsNoTracking()
                .Where(o => o.TenantId == tenantId && linkedOrderIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, ct);

        foreach (var stop in request.Stops)
        {
            if (string.IsNullOrWhiteSpace(stop.Address))
                throw new ArgumentException("Each stop requires a non-empty address.");
            if (stop.StopType == DeliveryStopType.Pickup
                && depotPoint.HasValue
                && DepotPickupMatcher.IsPickupAtDepot(stop.Address.Trim(), null, depotPoint.Value, depotAddress))
                continue;

            var recipientName = stop.RecipientName;
            var recipientPhone = stop.RecipientPhone;
            var parcelDescription = stop.ParcelDescription;
            if (stop.DeliveryOrderId.HasValue && linkedOrders.TryGetValue(stop.DeliveryOrderId.Value, out var linkedOrder))
            {
                if (string.IsNullOrWhiteSpace(recipientName))
                    recipientName = linkedOrder.RecipientName;
                if (string.IsNullOrWhiteSpace(recipientPhone))
                    recipientPhone = string.IsNullOrWhiteSpace(linkedOrder.RecipientPhone) ? null : linkedOrder.RecipientPhone;
                if (string.IsNullOrWhiteSpace(parcelDescription))
                    parcelDescription = string.IsNullOrWhiteSpace(linkedOrder.ParcelDescription) ? null : linkedOrder.ParcelDescription;
            }

            route.Stops.Add(new DeliveryRouteStop
            {
                Id = Guid.NewGuid(),
                RouteId = routeId,
                Sequence = seq++,
                StopType = stop.StopType,
                Status = DeliveryStopStatus.Pending,
                Address = stop.Address.Trim(),
                RecipientName = recipientName,
                RecipientPhone = recipientPhone,
                ParcelDescription = parcelDescription,
                Notes = stop.Notes,
                DeliveryOrderId = stop.DeliveryOrderId
            });
        }

        if (route.Stops.Count <= 1)
            throw new ArgumentException("At least one delivery stop is required.");

        db.DeliveryRoutes.Add(route);
        await db.SaveChangesAsync(ct);

        try
        {
            await RecordDeliveryEventAsync(
                tenantId,
                DeliveryEventTypes.RouteCreated,
                orderId: null,
                routeId: routeId,
                narrative: $"Route {route.Name} created with {request.Stops.Count} stop(s).",
                metrics: new Dictionary<string, object?> { ["stopCount"] = request.Stops.Count },
                ct: ct);
        }
        catch
        {
            // Route is saved; insights event failure must not block creation.
        }

        return (await GetRouteAsync(routeId, ct))!;
    }

    public async Task<DeliveryRouteDetailDto> UpdateRouteAsync(Guid routeId, UpdateDeliveryRouteRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        EnsureRouteEditable(route);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Route name is required.");

        var depotAddress = await depots.ResolveDepotAddressAsync(route.TenantId, request.DepotId, request.DepotAddress, ct);

        route.Name = request.Name.Trim();
        route.DepotAddress = depotAddress;
        route.ScheduledDate = request.ScheduledDate;

        var depot = route.Stops.FirstOrDefault(s => s.StopType == DeliveryStopType.Depot);
        if (depot is not null)
            depot.Address = route.DepotAddress;

        await db.SaveChangesAsync(ct);
        return (await GetRouteAsync(routeId, ct))!;
    }

    public async Task<DeliveryRouteDetailDto> UpdateRouteStatusAsync(Guid routeId, UpdateRouteStatusRequest request, CancellationToken ct = default)
    {
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        var deliveryStops = route.Stops.Where(s => s.StopType != DeliveryStopType.Depot).ToList();

        switch (request.Status)
        {
            case DeliveryRouteStatus.Planned when route.Status == DeliveryRouteStatus.Draft:
                break;
            case DeliveryRouteStatus.InProgress when route.Status == DeliveryRouteStatus.Planned:
                EnsureRouteAssignedForStart(route);
                route.StartedAt ??= DateTime.UtcNow;
                var depot = route.Stops.FirstOrDefault(s => s.StopType == DeliveryStopType.Depot);
                if (depot is { Status: DeliveryStopStatus.Pending })
                {
                    depot.Status = DeliveryStopStatus.Completed;
                    depot.CompletedAt = DateTime.UtcNow;
                }
                await MarkRouteOrdersInTransitOnStartAsync(route, ct);
                break;
            case DeliveryRouteStatus.Completed when route.Status == DeliveryRouteStatus.InProgress:
                if (deliveryStops.Any(s => s.Status == DeliveryStopStatus.Pending))
                    throw new InvalidOperationException("All stops must be completed or skipped before completing the route.");
                route.CompletedAt = DateTime.UtcNow;
                break;
            case DeliveryRouteStatus.Cancelled when route.Status is not DeliveryRouteStatus.Completed:
                await ReleaseOrdersFromCancelledRouteAsync(route, ct);
                break;
            default:
                throw new InvalidOperationException($"Cannot transition route from {route.Status} to {request.Status}.");
        }

        route.Status = request.Status;
        await db.SaveChangesAsync(ct);

        if (request.Status == DeliveryRouteStatus.InProgress)
        {
            string? vehicleNumber = null;
            string? driverLabel = null;
            if (route.VehicleId.HasValue)
                vehicleNumber = await db.Vehicles.AsNoTracking().Where(v => v.Id == route.VehicleId).Select(v => v.VehicleNumber).FirstOrDefaultAsync(ct);
            if (route.DriverId.HasValue)
                driverLabel = await db.Drivers.AsNoTracking().Where(d => d.Id == route.DriverId).Select(d => d.DisplayName).FirstOrDefaultAsync(ct);
            await RecordDeliveryEventAsync(
                route.TenantId,
                DeliveryEventTypes.RouteStarted,
                orderId: null,
                routeId: route.Id,
                stopId: null,
                vehicleId: route.VehicleId,
                driverId: route.DriverId,
                vehicleLabel: vehicleNumber,
                driverLabel: driverLabel,
                narrative: driverLabel is not null
                    ? $"Route {route.Name} started with {driverLabel}."
                    : $"Route {route.Name} started.",
                ct: ct);
        }
        else if (request.Status == DeliveryRouteStatus.Completed)
        {
            string? vehicleNumber = null;
            string? driverLabel = null;
            if (route.VehicleId.HasValue)
                vehicleNumber = await db.Vehicles.AsNoTracking().Where(v => v.Id == route.VehicleId).Select(v => v.VehicleNumber).FirstOrDefaultAsync(ct);
            if (route.DriverId.HasValue)
                driverLabel = await db.Drivers.AsNoTracking().Where(d => d.Id == route.DriverId).Select(d => d.DisplayName).FirstOrDefaultAsync(ct);
            await RecordDeliveryEventAsync(
                route.TenantId,
                DeliveryEventTypes.RouteCompleted,
                orderId: null,
                routeId: route.Id,
                stopId: null,
                vehicleId: route.VehicleId,
                driverId: route.DriverId,
                vehicleLabel: vehicleNumber,
                driverLabel: driverLabel,
                narrative: driverLabel is not null
                    ? $"Route {route.Name} completed by {driverLabel}."
                    : $"Route {route.Name} completed.",
                ct: ct);
        }
        else if (request.Status == DeliveryRouteStatus.Cancelled)
        {
            try
            {
                await RecordDeliveryEventAsync(
                    route.TenantId,
                    DeliveryEventTypes.RouteCancelled,
                    orderId: null,
                    routeId: route.Id,
                    narrative: $"Route {route.Name} cancelled; orders returned to ready.",
                    ct: ct);
            }
            catch
            {
                // Route and orders are saved; insights event is best-effort.
            }
        }

        return (await GetRouteAsync(routeId, ct))!;
    }

    public async Task<DeliveryRouteDetailDto> AssignRouteAsync(Guid routeId, AssignRouteRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        EnsureRouteEditable(route);
        await ValidateVehicleForAssignmentAsync(route.TenantId, request.VehicleId, request.AutomationMode, ct);
        var driverId = await ResolveDriverIdForAssignmentAsync(route.TenantId, request.AutomationMode, request.DriverId, ct);

        route.VehicleId = request.VehicleId;
        route.DriverId = driverId;
        route.AutomationMode = request.AutomationMode;
        await SyncRouteOrderAssignmentsAsync(route, ct, recordEvents: true);
        await db.SaveChangesAsync(ct);

        var vehicleNumber = await db.Vehicles.AsNoTracking().Where(v => v.Id == request.VehicleId).Select(v => v.VehicleNumber).FirstAsync(ct);
        string? driverLabel = null;
        if (driverId.HasValue)
            driverLabel = await db.Drivers.AsNoTracking().Where(d => d.Id == driverId).Select(d => d.DisplayName).FirstOrDefaultAsync(ct);

        try
        {
            await RecordDeliveryEventAsync(
                route.TenantId,
                DeliveryEventTypes.RouteAssigned,
                orderId: null,
                routeId: route.Id,
                vehicleId: request.VehicleId,
                driverId: driverId,
                vehicleLabel: vehicleNumber,
                driverLabel: driverLabel,
                narrative: driverLabel is not null
                    ? $"Route {route.Name} assigned to {driverLabel} on {vehicleNumber}."
                    : $"Route {route.Name} assigned to vehicle {vehicleNumber}.",
                metrics: new Dictionary<string, object?> { ["automationMode"] = request.AutomationMode.ToString() },
                ct: ct);
        }
        catch
        {
            // Assignment saved; insights event is best-effort.
        }

        return (await GetRouteAsync(routeId, ct))!;
    }

    public async Task SyncRouteOrderAssignmentsAsync(Guid routeId, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        await SyncRouteOrderAssignmentsAsync(route, ct, recordEvents: true);
        await db.SaveChangesAsync(ct);
    }

    public async Task<DeliveryRouteStopDto> AddRouteStopAsync(Guid routeId, AddRouteStopRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        EnsureRouteEditable(route);
        if (request.StopType == DeliveryStopType.Depot)
            throw new InvalidOperationException("Depot stops cannot be added manually.");
        if (string.IsNullOrWhiteSpace(request.Address))
            throw new ArgumentException("Stop address is required.");

        var maxSeq = route.Stops.Max(s => s.Sequence);
        var stop = new DeliveryRouteStop
        {
            Id = Guid.NewGuid(),
            RouteId = routeId,
            Sequence = maxSeq + 1,
            StopType = request.StopType,
            Status = DeliveryStopStatus.Pending,
            Address = request.Address.Trim(),
            RecipientName = request.RecipientName,
            RecipientPhone = request.RecipientPhone,
            ParcelDescription = request.ParcelDescription,
            Notes = request.Notes
        };
        route.Stops.Add(stop);
        db.DeliveryRouteStops.Add(stop);
        await db.SaveChangesAsync(ct);
        return MapStop(stop);
    }

    public async Task<DeliveryRouteStopDto> UpdateRouteStopAsync(Guid routeId, Guid stopId, UpdateRouteStopRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        EnsureRouteEditable(route);
        var stop = route.Stops.FirstOrDefault(s => s.Id == stopId)
            ?? throw new InvalidOperationException("Stop not found.");
        if (stop.StopType == DeliveryStopType.Depot)
            throw new InvalidOperationException("Depot stop cannot be modified here; update the route depot address instead.");
        if (string.IsNullOrWhiteSpace(request.Address))
            throw new ArgumentException("Stop address is required.");

        stop.Address = request.Address.Trim();
        stop.RecipientName = request.RecipientName;
        stop.RecipientPhone = request.RecipientPhone;
        stop.ParcelDescription = request.ParcelDescription;
        stop.Notes = request.Notes;
        await db.SaveChangesAsync(ct);
        return MapStop(stop);
    }

    public async Task<DeliveryRouteDetailDto> ReorderRouteStopsAsync(Guid routeId, ReorderRouteStopsRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        EnsureRouteEditable(route);

        var deliveryStops = route.Stops.Where(s => s.StopType != DeliveryStopType.Depot).ToList();
        if (request.StopIds.Count != deliveryStops.Count)
            throw new InvalidOperationException("Reorder must include all delivery stops exactly once.");
        if (request.StopIds.Distinct().Count() != request.StopIds.Count)
            throw new InvalidOperationException("Duplicate stop IDs in reorder request.");

        var stopById = deliveryStops.ToDictionary(s => s.Id);
        foreach (var id in request.StopIds)
        {
            if (!stopById.ContainsKey(id))
                throw new InvalidOperationException("Stop not found on this route.");
        }

        // Unique index on (RouteId, Sequence) — assign temporary sequences first to avoid conflicts.
        await ApplyStopSequencesAsync(request.StopIds.Select(id => stopById[id]).ToList(), ct);
        return (await GetRouteAsync(routeId, ct))!;
    }

    private async Task ApplyStopSequencesAsync(IReadOnlyList<DeliveryRouteStop> stopsInOrder, CancellationToken ct)
    {
        var tempSeq = -1;
        foreach (var stop in stopsInOrder)
            stop.Sequence = tempSeq--;
        await db.SaveChangesAsync(ct);

        var seq = 1;
        foreach (var stop in stopsInOrder)
            stop.Sequence = seq++;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteRouteStopAsync(Guid routeId, Guid stopId, CancellationToken ct = default)
    {
        currentUser.EnsureDispatcher();
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        EnsureRouteEditable(route);
        var stop = route.Stops.FirstOrDefault(s => s.Id == stopId)
            ?? throw new InvalidOperationException("Stop not found.");
        if (stop.StopType == DeliveryStopType.Depot)
            throw new InvalidOperationException("Depot stop cannot be deleted.");

        route.Stops.Remove(stop);
        db.DeliveryRouteStops.Remove(stop);

        var remaining = route.Stops.Where(s => s.StopType != DeliveryStopType.Depot).OrderBy(s => s.Sequence).ToList();
        await ApplyStopSequencesAsync(remaining, ct);
    }

    public async Task<DeliveryRouteStopDto> UpdateStopStatusAsync(Guid routeId, Guid stopId, UpdateStopStatusRequest request, CancellationToken ct = default)
    {
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        if (route.Status != DeliveryRouteStatus.InProgress)
            throw new InvalidOperationException("Stops can only be updated while the route is in progress.");

        var stop = route.Stops.FirstOrDefault(s => s.Id == stopId)
            ?? throw new InvalidOperationException("Stop not found.");
        if (stop.StopType == DeliveryStopType.Depot)
            throw new InvalidOperationException("Depot stop status is managed by the route lifecycle.");

        var next = route.Stops
            .Where(s => s.StopType != DeliveryStopType.Depot)
            .OrderBy(s => s.Sequence)
            .FirstOrDefault(s => s.Status == DeliveryStopStatus.Pending);
        if (next is null || next.Id != stopId)
            throw new InvalidOperationException("Stops must be completed or skipped in sequence.");

        if (request.Status is not DeliveryStopStatus.Completed and not DeliveryStopStatus.Skipped)
            throw new InvalidOperationException("Stop status must be Completed or Skipped.");

        stop.Status = request.Status;
        stop.CompletedAt = DateTime.UtcNow;

        DeliveryOrder? linkedOrder = null;
        DeliveryOrderStatus? previousOrderStatus = null;
        if (request.Status == DeliveryStopStatus.Completed && stop.DeliveryOrderId.HasValue)
        {
            linkedOrder = await db.DeliveryOrders
                .Include(o => o.Assignment)
                .FirstOrDefaultAsync(o => o.Id == stop.DeliveryOrderId.Value, ct);
            if (linkedOrder is not null)
            {
                previousOrderStatus = linkedOrder.Status;
                ApplyOrderStatusFromCompletedStop(linkedOrder, stop.StopType);
                if (stop.StopType == DeliveryStopType.Pickup
                    && linkedOrder.Status == DeliveryOrderStatus.PickedUp
                    && HasPendingDropoffOnRoute(route, linkedOrder.Id, route.DepotAddress))
                {
                    linkedOrder.Status = DeliveryOrderStatus.InTransit;
                }
            }
        }

        await db.SaveChangesAsync(ct);

        string? vehicleNumber = null;
        string? driverLabel = null;
        if (route.VehicleId.HasValue)
            vehicleNumber = await db.Vehicles.AsNoTracking().Where(v => v.Id == route.VehicleId).Select(v => v.VehicleNumber).FirstOrDefaultAsync(ct);
        if (route.DriverId.HasValue)
            driverLabel = await db.Drivers.AsNoTracking().Where(d => d.Id == route.DriverId).Select(d => d.DisplayName).FirstOrDefaultAsync(ct);

        await RecordDeliveryEventAsync(
            route.TenantId,
            DeliveryEventTypes.StopCompleted,
            orderId: stop.DeliveryOrderId,
            routeId: route.Id,
            stopId: stop.Id,
            vehicleId: route.VehicleId,
            driverId: route.DriverId,
            customerId: linkedOrder?.CustomerId,
            vehicleLabel: vehicleNumber,
            driverLabel: driverLabel,
            customerLabel: linkedOrder?.RecipientName,
            narrative: $"Stop #{stop.Sequence} ({stop.StopType}) marked {stop.Status}.",
            metrics: new Dictionary<string, object?> { ["sequence"] = stop.Sequence, ["stopType"] = stop.StopType.ToString() },
            ct: ct);

        if (linkedOrder is not null && previousOrderStatus != linkedOrder.Status)
        {
            var orderEventType = linkedOrder.Status switch
            {
                DeliveryOrderStatus.Delivered => DeliveryEventTypes.OrderDelivered,
                DeliveryOrderStatus.Failed => DeliveryEventTypes.OrderFailed,
                _ => DeliveryEventTypes.OrderStatusChanged
            };
            var orderNarrative = linkedOrder.Status switch
            {
                DeliveryOrderStatus.Delivered =>
                    $"Order delivered to {linkedOrder.RecipientName} at {linkedOrder.DeliveryAddress}.",
                DeliveryOrderStatus.PickedUp =>
                    $"Order picked up for {linkedOrder.RecipientName}.",
                _ => $"Order status changed from {previousOrderStatus} to {linkedOrder.Status}."
            };

            await RecordDeliveryEventAsync(
                route.TenantId,
                orderEventType,
                orderId: linkedOrder.Id,
                routeId: route.Id,
                stopId: stop.Id,
                vehicleId: route.VehicleId,
                driverId: route.DriverId,
                customerId: linkedOrder.CustomerId,
                vehicleLabel: vehicleNumber,
                driverLabel: driverLabel,
                customerLabel: linkedOrder.RecipientName,
                narrative: orderNarrative,
                context: new Dictionary<string, object?>
                {
                    ["status"] = linkedOrder.Status.ToString(),
                    ["previousStatus"] = previousOrderStatus?.ToString()
                },
                ct: ct);
        }

        return MapStop(stop);
    }

    private static bool HasPendingDropoffOnRoute(DeliveryRoute route, Guid orderId, string depotAddress) =>
        route.Stops.Any(s =>
            s.DeliveryOrderId == orderId
            && s.StopType == DeliveryStopType.Dropoff
            && s.Status == DeliveryStopStatus.Pending
            && CountsAsOperationalStop(s, depotAddress));

    private async Task ReleaseOrdersFromCancelledRouteAsync(DeliveryRoute route, CancellationToken ct)
    {
        foreach (var stop in route.Stops.Where(s => s.Status == DeliveryStopStatus.Pending))
            stop.Status = DeliveryStopStatus.Skipped;

        var orderIds = route.Stops
            .Where(s => s.DeliveryOrderId.HasValue)
            .Select(s => s.DeliveryOrderId!.Value)
            .Distinct()
            .ToList();
        if (orderIds.Count == 0)
            return;

        var orders = await db.DeliveryOrders
            .Include(o => o.Assignment)
            .Where(o => orderIds.Contains(o.Id))
            .ToListAsync(ct);

        foreach (var order in orders)
        {
            if (order.Status is DeliveryOrderStatus.Delivered
                or DeliveryOrderStatus.Failed
                or DeliveryOrderStatus.Cancelled)
                continue;

            var previousStatus = order.Status;
            var hadAssignment = order.Assignment is not null;

            if (order.Assignment is not null)
            {
                db.DeliveryAssignments.Remove(order.Assignment);
                order.Assignment = null;
            }

            if (order.Status != DeliveryOrderStatus.Created)
                order.Status = DeliveryOrderStatus.Created;

            if (!hadAssignment && previousStatus == DeliveryOrderStatus.Created)
                continue;

            try
            {
                await RecordDeliveryEventAsync(
                    route.TenantId,
                    DeliveryEventTypes.OrderStatusChanged,
                    orderId: order.Id,
                    routeId: route.Id,
                    customerId: order.CustomerId,
                    customerLabel: order.RecipientName,
                    narrative: $"Order released from cancelled route {route.Name}; returned to ready.",
                    context: new Dictionary<string, object?>
                    {
                        ["status"] = order.Status.ToString(),
                        ["previousStatus"] = previousStatus.ToString()
                    },
                    ct: ct);
            }
            catch
            {
                // Order release is persisted; event logging is best-effort.
            }
        }
    }

    private async Task MarkRouteOrdersInTransitOnStartAsync(DeliveryRoute route, CancellationToken ct)
    {
        await SyncRouteOrderAssignmentsAsync(route, ct, recordEvents: true);

        GeoPoint? depotPoint = null;
        if (!string.IsNullOrWhiteSpace(route.DepotAddress))
            depotPoint = (await orderGeocoding.GeocodeAddressAsync(route.DepotAddress, route.TenantId, ct))?.ToPoint();

        var orderIds = route.Stops
            .Where(s => s.DeliveryOrderId.HasValue)
            .Select(s => s.DeliveryOrderId!.Value)
            .Distinct()
            .ToList();
        if (orderIds.Count == 0)
            return;

        var orders = await db.DeliveryOrders
            .Include(o => o.Assignment)
            .Where(o => orderIds.Contains(o.Id))
            .ToListAsync(ct);

        string? vehicleNumber = null;
        string? driverLabel = null;
        if (route.VehicleId.HasValue)
            vehicleNumber = await db.Vehicles.AsNoTracking().Where(v => v.Id == route.VehicleId).Select(v => v.VehicleNumber).FirstOrDefaultAsync(ct);
        if (route.DriverId.HasValue)
            driverLabel = await db.Drivers.AsNoTracking().Where(d => d.Id == route.DriverId).Select(d => d.DisplayName).FirstOrDefaultAsync(ct);

        foreach (var order in orders)
        {
            if (order.Status is not DeliveryOrderStatus.Assigned and not DeliveryOrderStatus.PickedUp)
                continue;

            var hasPendingPickup = route.Stops.Any(s =>
                s.DeliveryOrderId == order.Id
                && s.StopType == DeliveryStopType.Pickup
                && s.Status == DeliveryStopStatus.Pending
                && CountsAsOperationalStop(s, route.DepotAddress, depotPoint));

            if (order.Status == DeliveryOrderStatus.Assigned && hasPendingPickup)
                continue;

            var previous = order.Status;
            order.Status = DeliveryOrderStatus.InTransit;
            await RecordDeliveryEventAsync(
                route.TenantId,
                DeliveryEventTypes.OrderStatusChanged,
                orderId: order.Id,
                routeId: route.Id,
                vehicleId: route.VehicleId,
                driverId: route.DriverId,
                customerId: order.CustomerId,
                vehicleLabel: vehicleNumber,
                driverLabel: driverLabel,
                customerLabel: order.RecipientName,
                narrative: $"Order en route for {order.RecipientName}.",
                context: new Dictionary<string, object?>
                {
                    ["status"] = order.Status.ToString(),
                    ["previousStatus"] = previous.ToString()
                },
                ct: ct);
        }
    }

    private async Task SyncRouteOrderAssignmentsAsync(
        DeliveryRoute route,
        CancellationToken ct,
        bool recordEvents = false)
    {
        if (!route.VehicleId.HasValue)
            return;

        var automationMode = route.AutomationMode ?? AutomationMode.Conventional;
        var orderIds = route.Stops
            .Where(s => s.DeliveryOrderId.HasValue)
            .Select(s => s.DeliveryOrderId!.Value)
            .Distinct()
            .ToList();
        if (orderIds.Count == 0)
            return;

        var orders = await db.DeliveryOrders
            .Include(o => o.Assignment)
            .Where(o => orderIds.Contains(o.Id))
            .ToListAsync(ct);

        string? vehicleNumber = await db.Vehicles.AsNoTracking()
            .Where(v => v.Id == route.VehicleId.Value)
            .Select(v => v.VehicleNumber)
            .FirstOrDefaultAsync(ct);
        string? driverLabel = null;
        if (route.DriverId.HasValue)
        {
            driverLabel = await db.Drivers.AsNoTracking()
                .Where(d => d.Id == route.DriverId.Value)
                .Select(d => d.DisplayName)
                .FirstOrDefaultAsync(ct);
        }

        foreach (var order in orders)
        {
            if (order.Status is DeliveryOrderStatus.Delivered
                or DeliveryOrderStatus.Failed
                or DeliveryOrderStatus.Cancelled)
                continue;

            var created = order.Assignment is null;
            var changed = !created && (
                order.Assignment!.VehicleId != route.VehicleId.Value
                || order.Assignment.DriverId != route.DriverId
                || order.Assignment.AutomationMode != automationMode);

            if (created)
            {
                order.Assignment = new DeliveryAssignment
                {
                    Id = Guid.NewGuid(),
                    DeliveryOrderId = order.Id,
                    VehicleId = route.VehicleId.Value,
                    DriverId = route.DriverId,
                    AutomationMode = automationMode,
                    AssignedAt = DateTime.UtcNow
                };
                db.DeliveryAssignments.Add(order.Assignment);
            }
            else if (changed)
            {
                order.Assignment!.VehicleId = route.VehicleId.Value;
                order.Assignment.DriverId = route.DriverId;
                order.Assignment.AutomationMode = automationMode;
                order.Assignment.AssignedAt = DateTime.UtcNow;
            }

            if (order.Status == DeliveryOrderStatus.Created)
                order.Status = DeliveryOrderStatus.Assigned;

            if (recordEvents && (created || changed))
            {
                await RecordDeliveryEventAsync(
                    route.TenantId,
                    DeliveryEventTypes.OrderAssigned,
                    orderId: order.Id,
                    routeId: route.Id,
                    vehicleId: route.VehicleId,
                    driverId: route.DriverId,
                    customerId: order.CustomerId,
                    vehicleLabel: vehicleNumber,
                    driverLabel: driverLabel,
                    customerLabel: order.RecipientName,
                    narrative: driverLabel is not null
                        ? $"Order assigned to {driverLabel} on vehicle {vehicleNumber}."
                        : $"Order assigned to vehicle {vehicleNumber}.",
                    metrics: new Dictionary<string, object?> { ["automationMode"] = automationMode.ToString() },
                    ct: ct);
            }
        }
    }

    private static void ApplyOrderStatusFromCompletedStop(DeliveryOrder order, DeliveryStopType stopType)
    {
        switch (stopType)
        {
            case DeliveryStopType.Pickup when order.Status is DeliveryOrderStatus.Created
                or DeliveryOrderStatus.Assigned
                or DeliveryOrderStatus.InTransit:
                order.Status = DeliveryOrderStatus.PickedUp;
                break;
            case DeliveryStopType.Dropoff when order.Status is not DeliveryOrderStatus.Delivered
                and not DeliveryOrderStatus.Failed
                and not DeliveryOrderStatus.Cancelled:
                order.Status = DeliveryOrderStatus.Delivered;
                order.DeliveredAt = DateTime.UtcNow;
                if (order.Assignment is not null)
                    order.Assignment.CompletedAt = DateTime.UtcNow;
                break;
        }
    }

    public async Task<IReadOnlyList<DeliveryOrderDto>> GetPartnerOrdersAsync(CancellationToken ct = default)
    {
        var tenantId = await RequirePartnerDeliveryTenantIdAsync(ct);
        return await GetOrdersAsync(tenantId, ct);
    }

    public async Task<DeliveryOrderDto?> GetPartnerOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        await RequirePartnerDeliveryTenantIdAsync(ct);
        var order = await GetOrderAsync(orderId, ct);
        if (order is null) return null;
        if (!currentUser.IsPlatformAdmin && order.TenantId != currentUser.TenantId)
            throw new ForbiddenException("You do not have access to this order.");
        return order;
    }

    public async Task<DeliveryOrderDto> CreatePartnerOrderAsync(CreateDeliveryOrderRequest request, CancellationToken ct = default)
    {
        var tenantId = await RequirePartnerDeliveryTenantIdAsync(ct);
        return await CreateOrderAsync(tenantId, request, ct);
    }

    private async Task<Guid> RequirePartnerDeliveryTenantIdAsync(CancellationToken ct)
    {
        if (!currentUser.IsApiKeyAuth)
            throw new ForbiddenException("Partner API requires an API key.");
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Invalid API key context.");
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new ForbiddenException("Tenant not found.");
        if (!ProductModuleHelper.HasModule(tenant.Modules, ProductModule.Delivery))
            throw new ForbiddenException("Delivery module is required.");
        return tenantId;
    }

    private IQueryable<DeliveryOrder> ScopedOrders()
    {
        var query = db.DeliveryOrders.AsQueryable();
        if (!currentUser.IsPlatformAdmin && currentUser.TenantId.HasValue)
            query = query.Where(o => o.TenantId == currentUser.TenantId.Value);
        return query;
    }

    private IQueryable<DeliveryRoute> ScopedRoutes()
    {
        var query = db.DeliveryRoutes.AsQueryable();
        if (!currentUser.IsPlatformAdmin && currentUser.TenantId.HasValue)
            query = query.Where(r => r.TenantId == currentUser.TenantId.Value);
        if (currentUser.IsDriver && currentUser.DriverId.HasValue)
            query = query.Where(r => r.DriverId == currentUser.DriverId.Value);
        return query;
    }

    private async Task<List<Guid>> ScopedDeliveryTenantIdsAsync(CancellationToken ct)
    {
        var query = db.Tenants.AsNoTracking().Where(f => (f.Modules & ProductModule.Delivery) == ProductModule.Delivery);
        if (!currentUser.IsPlatformAdmin && currentUser.TenantId.HasValue)
            query = query.Where(f => f.Id == currentUser.TenantId.Value);
        return await query.Select(f => f.Id).ToListAsync(ct);
    }

    private async Task<DeliveryRoute> LoadRouteForUpdateAsync(Guid routeId, CancellationToken ct)
    {
        var route = await ScopedRoutes()
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == routeId, ct)
            ?? throw new InvalidOperationException("Route not found.");
        return route;
    }

    private static void EnsureRouteEditable(DeliveryRoute route)
    {
        if (route.Status is not DeliveryRouteStatus.Draft and not DeliveryRouteStatus.Planned)
            throw new InvalidOperationException("Route can only be modified while in Draft or Planned status.");
    }

    private static void EnsureRouteAssignedForStart(DeliveryRoute route)
    {
        if (!route.VehicleId.HasValue)
            throw new InvalidOperationException("Assign a vehicle before starting the route.");

        var mode = route.AutomationMode ?? AutomationMode.Conventional;
        if (mode != AutomationMode.Autonomous && !route.DriverId.HasValue)
            throw new InvalidOperationException("Assign a driver before starting the route.");
    }

    private async Task ValidateVehicleForAssignmentAsync(Guid tenantId, Guid vehicleId, AutomationMode mode, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehicle not found.");
        if (vehicle.TenantId != tenantId)
            throw new InvalidOperationException("Vehicle does not belong to this tenant.");

        if (mode == AutomationMode.Autonomous)
        {
            var profile = await db.RoboTaxiProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.VehicleId == vehicleId, ct);
            if (profile is null)
                throw new InvalidOperationException("Autonomous mode requires a vehicle with a robo-taxi profile.");
            if (profile.OperationalState == OperationalState.Grounded)
                throw new InvalidOperationException("Cannot assign a grounded autonomous vehicle.");
        }
    }

    private async Task<Guid?> ResolveDriverIdForAssignmentAsync(
        Guid tenantId,
        AutomationMode mode,
        Guid? driverId,
        CancellationToken ct)
    {
        if (mode == AutomationMode.Autonomous)
            return null;

        if (!driverId.HasValue)
            return null;

        var driver = await db.Drivers.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == driverId.Value && d.TenantId == tenantId, ct)
            ?? throw new ArgumentException("Driver not found.");
        if (!driver.IsActive)
            throw new ArgumentException("Driver is inactive.");
        return driver.Id;
    }

    private async Task<Dictionary<Guid, string>> DriverNamesAsync(IReadOnlyList<Guid> driverIds, CancellationToken ct)
    {
        if (driverIds.Count == 0)
            return [];
        return await db.Drivers.AsNoTracking()
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.DisplayName, ct);
    }

    private static bool CanEditOrder(DeliveryOrderStatus status) =>
        status is not DeliveryOrderStatus.Delivered
            and not DeliveryOrderStatus.Cancelled
            and not DeliveryOrderStatus.Failed;

    private static bool AddressesEqual(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? ResolveExternalRef(string? requestRef, string? customerRef)
    {
        if (!string.IsNullOrWhiteSpace(requestRef))
            return requestRef.Trim();
        if (!string.IsNullOrWhiteSpace(customerRef))
            return customerRef.Trim();
        return null;
    }

    private static bool NeedsGeocode(DeliveryOrder order) =>
        !order.PickupLatitude.HasValue || !order.PickupLongitude.HasValue
        || !order.DeliveryLatitude.HasValue || !order.DeliveryLongitude.HasValue;

    /// <summary>
    /// Fills in lat/lng only when missing. Does not call external geocoders to upgrade cached coordinates
    /// (that belongs on create/update and route planning, not on read paths).
    /// </summary>
    private async Task BackfillMissingCoordinatesOnlyAsync(IReadOnlyList<Guid> orderIds, CancellationToken ct)
    {
        if (orderIds.Count == 0) return;

        var tracked = await db.DeliveryOrders
            .Where(o => orderIds.Contains(o.Id))
            .ToListAsync(ct);

        foreach (var order in tracked)
        {
            if (!NeedsGeocode(order) && !await orderGeocoding.HasStaleOrderGeocodeAsync(order, ct))
                continue;

            await orderGeocoding.EnsureOrderCoordinatesAsync(order, ct, upgradeDeterministic: false);
        }
    }

    private readonly record struct VehicleDisplayInfo(string VehicleNumber, string LicensePlate);

    private async Task<Dictionary<Guid, VehicleDisplayInfo>> VehicleDisplayInfoAsync(IEnumerable<Guid> vehicleIds, CancellationToken ct)
    {
        var ids = vehicleIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await db.Vehicles.AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => new VehicleDisplayInfo(v.VehicleNumber, v.LicensePlate), ct);
    }

    private async Task<VehicleDisplayInfo?> GetVehicleDisplayInfoAsync(Guid vehicleId, CancellationToken ct)
    {
        var info = await db.Vehicles.AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => new { v.VehicleNumber, v.LicensePlate })
            .FirstOrDefaultAsync(ct);
        return info is null ? null : new VehicleDisplayInfo(info.VehicleNumber, info.LicensePlate);
    }

    private DeliveryOrderDto MapOrder(
        DeliveryOrder order,
        string? vehicleNumber,
        string? licensePlate,
        string? driverName,
        IReadOnlyDictionary<string, string>? geocodeSources = null,
        string? fixedRouteTemplateName = null) =>
        new(
            order.Id,
            order.TenantId,
            order.Status,
            order.PickupAddress,
            order.DeliveryAddress,
            order.RecipientName,
            order.RecipientPhone,
            order.ParcelDescription,
            order.PickupLatitude,
            order.PickupLongitude,
            order.DeliveryLatitude,
            order.DeliveryLongitude,
            geocodeSources is null ? null : orderGeocoding.ResolveCachedSource(order.PickupAddress, geocodeSources),
            geocodeSources is null ? null : orderGeocoding.ResolveCachedSource(order.DeliveryAddress, geocodeSources),
            order.PickupFormattedAddress,
            order.DeliveryFormattedAddress,
            order.ExternalRef,
            order.Assignment is null ? null : new DeliveryAssignmentDto(
                order.Assignment.Id,
                order.Assignment.VehicleId,
                vehicleNumber ?? string.Empty,
                licensePlate ?? string.Empty,
                order.Assignment.AutomationMode,
                order.Assignment.DriverId,
                driverName,
                order.Assignment.AssignedAt),
            order.CreatedAt,
            order.FixedRouteTemplateId,
            fixedRouteTemplateName,
            order.HeldUntil?.ToString("yyyy-MM-dd"));

    private async Task<IReadOnlyDictionary<Guid, string>> FixedRouteTemplateNamesAsync(
        IEnumerable<Guid> templateIds,
        CancellationToken ct)
    {
        var ids = templateIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await db.FixedRouteTemplates.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);
    }

    private static DeliveryRouteDto MapRouteSummary(
        DeliveryRoute route,
        string? vehicleNumber,
        string? licensePlate,
        string? driverName)
    {
        var (completed, pending) = CountDeliveryStopProgress(route.Stops, route.DepotAddress);
        return new DeliveryRouteDto(
            route.Id,
            route.TenantId,
            route.Name,
            route.Status,
            route.DepotAddress,
            route.ScheduledDate,
            route.VehicleId,
            vehicleNumber,
            licensePlate,
            route.DriverId,
            driverName,
            route.AutomationMode,
            route.Stops.Count(s => CountsAsOperationalStop(s, route.DepotAddress)),
            completed,
            pending,
            route.RoutePlanRunId,
            route.CreatedAt,
            route.StartedAt,
            route.CompletedAt);
    }

    private async Task<DeliveryRouteDetailDto> MapRouteDetailAsync(
        DeliveryRoute route,
        string? vehicleNumber,
        string? licensePlate,
        string? driverName,
        CancellationToken ct)
    {
        var orderIds = route.Stops
            .Where(s => s.DeliveryOrderId.HasValue)
            .Select(s => s.DeliveryOrderId!.Value)
            .Distinct()
            .ToList();
        var orders = orderIds.Count == 0
            ? new Dictionary<Guid, DeliveryOrder>()
            : await db.DeliveryOrders.AsNoTracking()
                .Where(o => orderIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, ct);

        GeoPoint? depotPoint = null;
        if (!string.IsNullOrWhiteSpace(route.DepotAddress))
            depotPoint = (await orderGeocoding.GeocodeAddressAsync(route.DepotAddress, route.TenantId, ct))?.ToPoint();

        var sharedHints = AddressGeocodeHints.Empty;
        foreach (var order in orders.Values)
            sharedHints = AddressGeocodeHints.Merge(sharedHints, AddressGeocodeHints.Parse(order.PickupAddress));

        var stops = new List<DeliveryRouteStopDto>();
        foreach (var stop in route.Stops.OrderBy(s => s.Sequence))
        {
            if (!CountsAsOperationalStop(stop, route.DepotAddress, depotPoint))
                continue;
            stops.Add(await MapStopAsync(stop, orders, depotPoint, sharedHints, route.TenantId, ct));
        }

        // Preserve visit order for drive-time estimates (do not re-optimize on read).
        stops.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));

        int? estimatedDriveMinutes = null;
        if (depotPoint.HasValue)
        {
            var planningStops = BuildPlanningStops(stops, route.DepotAddress, depotPoint);
            if (planningStops.Count > 0)
            {
                estimatedDriveMinutes = PickupDeliveryRouteOptimizer.EstimateRouteMinutes(
                    depotPoint.Value,
                    planningStops,
                    travelTimeMatrix,
                    planningOptions.Value.DefaultServiceMinutes);
            }
        }

        int? estimatedMinutesToNextStop = null;
        DateTime? estimatedNextStopArrivalAt = null;
        if (route.Status == DeliveryRouteStatus.InProgress && depotPoint.HasValue)
        {
            var nextStop = stops
                .Where(s => s.Status == DeliveryStopStatus.Pending)
                .OrderBy(s => s.Sequence)
                .FirstOrDefault();
            if (nextStop?.Latitude is not null && nextStop.Longitude is not null)
            {
                var origin = depotPoint.Value;
                var lastCompleted = stops
                    .Where(s => s.Status == DeliveryStopStatus.Completed)
                    .OrderByDescending(s => s.Sequence)
                    .FirstOrDefault();
                if (lastCompleted?.Latitude is not null && lastCompleted.Longitude is not null)
                    origin = new GeoPoint(lastCompleted.Latitude.Value, lastCompleted.Longitude.Value);

                var destination = new GeoPoint(nextStop.Latitude.Value, nextStop.Longitude.Value);
                estimatedMinutesToNextStop = travelTimeMatrix.TravelMinutes(origin, destination);
                estimatedNextStopArrivalAt = DateTime.UtcNow.AddMinutes(estimatedMinutesToNextStop.Value);
            }
        }

        return new DeliveryRouteDetailDto(
            route.Id,
            route.TenantId,
            route.Name,
            route.Status,
            route.DepotAddress,
            route.ScheduledDate,
            route.VehicleId,
            vehicleNumber,
            licensePlate,
            route.DriverId,
            driverName,
            route.AutomationMode,
            stops,
            estimatedDriveMinutes,
            estimatedMinutesToNextStop,
            estimatedNextStopArrivalAt,
            route.RoutePlanRunId,
            route.CreatedAt,
            route.StartedAt,
            route.CompletedAt);
    }

    private static List<PlanningStop> BuildPlanningStops(
        IReadOnlyList<DeliveryRouteStopDto> stops,
        string depotAddress,
        GeoPoint? depotPoint) =>
        stops
            .Where(s => s.StopType != DeliveryStopType.Depot
                && !IsDepotPickupStop(s.StopType, s.Address, depotAddress, depotPoint))
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .Select(s => new PlanningStop(
                s.DeliveryOrderId,
                s.StopType.ToString(),
                s.Address,
                new GeoPoint(s.Latitude!.Value, s.Longitude!.Value),
                s.RecipientName,
                s.ParcelDescription,
                s.Id))
            .ToList();

    private static bool CountsAsOperationalStop(
        DeliveryRouteStop stop,
        string depotAddress,
        GeoPoint? depotPoint = null) =>
        stop.StopType != DeliveryStopType.Depot
        && !IsDepotPickupStop(stop.StopType, stop.Address, depotAddress, depotPoint);

    private static bool IsDepotPickupStop(
        DeliveryStopType stopType,
        string address,
        string depotAddress,
        GeoPoint? depotPoint = null)
    {
        if (stopType != DeliveryStopType.Pickup)
            return false;

        if (depotPoint.HasValue)
            return DepotPickupMatcher.IsPickupAtDepot(address, null, depotPoint.Value, depotAddress);

        return DepotPickupMatcher.NormalizeAddress(address) == DepotPickupMatcher.NormalizeAddress(depotAddress);
    }

    private static (int Completed, int Pending) CountDeliveryStopProgress(
        IEnumerable<DeliveryRouteStop> stops,
        string depotAddress)
    {
        var delivery = stops.Where(s => CountsAsOperationalStop(s, depotAddress));
        return (
            delivery.Count(s => s.Status == DeliveryStopStatus.Completed),
            delivery.Count(s => s.Status == DeliveryStopStatus.Pending));
    }

    private async Task<DeliveryRouteStopDto> MapStopAsync(
        DeliveryRouteStop stop,
        IReadOnlyDictionary<Guid, DeliveryOrder> orders,
        GeoPoint? depotPoint,
        AddressGeocodeHints sharedHints,
        Guid tenantId,
        CancellationToken ct)
    {
        decimal? latitude = null;
        decimal? longitude = null;

        if (stop.StopType == DeliveryStopType.Depot && depotPoint.HasValue)
        {
            latitude = depotPoint.Value.Latitude;
            longitude = depotPoint.Value.Longitude;
        }
        else if (stop.DeliveryOrderId.HasValue && orders.TryGetValue(stop.DeliveryOrderId.Value, out var order))
        {
            if (stop.StopType == DeliveryStopType.Pickup)
            {
                latitude = order.PickupLatitude;
                longitude = order.PickupLongitude;
            }
            else
            {
                latitude = order.DeliveryLatitude;
                longitude = order.DeliveryLongitude;
            }
        }

        if (!latitude.HasValue || !longitude.HasValue)
        {
            var hints = AddressGeocodeHints.Merge(sharedHints, AddressGeocodeHints.Parse(stop.Address));
            var point = await orderGeocoding.GeocodeAddressAsync(stop.Address, tenantId, ct, hints);
            if (point.HasValue)
            {
                latitude = point.Value.Latitude;
                longitude = point.Value.Longitude;
            }
        }

        var parcelDescription = stop.ParcelDescription;
        if (string.IsNullOrWhiteSpace(parcelDescription)
            && stop.DeliveryOrderId.HasValue
            && orders.TryGetValue(stop.DeliveryOrderId.Value, out var linkedOrder)
            && !string.IsNullOrWhiteSpace(linkedOrder.ParcelDescription))
            parcelDescription = linkedOrder.ParcelDescription;

        return new DeliveryRouteStopDto(
            stop.Id,
            stop.Sequence,
            stop.StopType,
            stop.Status,
            stop.Address,
            stop.RecipientName,
            stop.RecipientPhone,
            parcelDescription,
            stop.Notes,
            stop.DeliveryOrderId,
            stop.CompletedAt,
            latitude,
            longitude);
    }

    private static DeliveryRouteStopDto MapStop(DeliveryRouteStop stop) =>
        new(
            stop.Id,
            stop.Sequence,
            stop.StopType,
            stop.Status,
            stop.Address,
            stop.RecipientName,
            stop.RecipientPhone,
            stop.ParcelDescription,
            stop.Notes,
            stop.DeliveryOrderId,
            stop.CompletedAt);

    private Task RecordOrderEventAsync(DeliveryOrder order, string eventType, string narrative, CancellationToken ct) =>
        RecordDeliveryEventAsync(
            order.TenantId,
            eventType,
            orderId: order.Id,
            routeId: null,
            stopId: null,
            vehicleId: null,
            driverId: null,
            customerId: order.CustomerId,
            customerLabel: order.RecipientName,
            narrative: narrative,
            context: new Dictionary<string, object?> { ["status"] = order.Status.ToString() },
            ct: ct);

    private Task RecordDeliveryEventAsync(
        Guid tenantId,
        string eventType,
        Guid? orderId = null,
        Guid? routeId = null,
        Guid? stopId = null,
        Guid? vehicleId = null,
        Guid? driverId = null,
        Guid? customerId = null,
        string? vehicleLabel = null,
        string? driverLabel = null,
        string? customerLabel = null,
        string? narrative = null,
        IReadOnlyDictionary<string, object?>? metrics = null,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken ct = default) =>
        events.RecordAsync(new RecordOperationalEventRequest(
            TenantId: tenantId,
            Domain: InsightsDomains.Delivery,
            EventType: eventType,
            OrderId: orderId,
            RouteId: routeId,
            StopId: stopId,
            VehicleId: vehicleId,
            DriverId: driverId,
            CustomerId: customerId,
            VehicleLabel: vehicleLabel,
            DriverLabel: driverLabel,
            CustomerLabel: customerLabel,
            Narrative: narrative,
            Metrics: metrics,
            Context: context), ct);
}
