using Microsoft.EntityFrameworkCore;
using UniConnect.Application.DTOs;
using UniConnect.Application.Interfaces;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Entities;
using UniConnect.Delivery.Enums;
using UniConnect.Delivery.Interfaces;
using UniConnect.Domain.Entities;
using UniConnect.Domain.Enums;
using UniConnect.Infrastructure.Data;
using UniConnect.RoboTaxi.Entities;
using UniConnect.RoboTaxi.Enums;

namespace UniConnect.Infrastructure.Services;

public class DeliveryService(AppDbContext db, ICurrentUserService currentUser) : IDeliveryService
{
    private static readonly DeliveryOrderStatus[] ActiveOrderStatuses =
    [
        DeliveryOrderStatus.Assigned,
        DeliveryOrderStatus.PickedUp,
        DeliveryOrderStatus.InTransit
    ];

    public async Task<DeliveryDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var fleetIds = await ScopedDeliveryFleetIdsAsync(ct);
        var orders = await ScopedOrders()
            .Where(o => fleetIds.Contains(o.FleetId))
            .ToListAsync(ct);

        var assignments = await db.DeliveryAssignments.AsNoTracking()
            .Where(a => orders.Select(o => o.Id).Contains(a.DeliveryOrderId))
            .ToListAsync(ct);

        var activeAssignmentOrderIds = orders
            .Where(o => ActiveOrderStatuses.Contains(o.Status))
            .Select(o => o.Id)
            .ToHashSet();

        var routes = await ScopedRoutes()
            .Where(r => fleetIds.Contains(r.FleetId))
            .ToListAsync(ct);

        return new DeliveryDashboardDto(
            orders.Count(o => o.Status == DeliveryOrderStatus.Created),
            orders.Count(o => o.Status == DeliveryOrderStatus.InTransit),
            orders.Count(o => o.Channel == DeliveryChannel.B2B),
            orders.Count(o => o.Channel == DeliveryChannel.B2C),
            assignments.Count(a => activeAssignmentOrderIds.Contains(a.DeliveryOrderId) && a.AutomationMode == AutomationMode.Autonomous),
            assignments.Count(a => activeAssignmentOrderIds.Contains(a.DeliveryOrderId) && a.AutomationMode == AutomationMode.Conventional),
            routes.Count(r => r.Status == DeliveryRouteStatus.InProgress),
            routes.Count(r => r.Status == DeliveryRouteStatus.Planned));
    }

    public async Task<IReadOnlyList<FleetDto>> GetFleetsAsync(CancellationToken ct = default)
    {
        var query = db.Fleets.AsNoTracking().Where(f => f.FleetType == FleetType.Delivery);
        if (!currentUser.IsPlatformAdmin && currentUser.FleetId.HasValue)
            query = query.Where(f => f.Id == currentUser.FleetId.Value);
        return await query.OrderBy(f => f.Name)
            .Select(f => new FleetDto(f.Id, f.Name, f.Slug, f.FleetType, f.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<FleetDto> CreateFleetAsync(string name, string slug, CancellationToken ct = default)
    {
        if (!currentUser.IsPlatformAdmin)
            throw new UnauthorizedAccessException("Only platform administrators can create fleets.");

        var fleet = new Fleet
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            FleetType = FleetType.Delivery,
            CreatedAt = DateTime.UtcNow
        };
        db.Fleets.Add(fleet);
        await db.SaveChangesAsync(ct);
        return new FleetDto(fleet.Id, fleet.Name, fleet.Slug, fleet.FleetType, fleet.CreatedAt);
    }

    public async Task<IReadOnlyList<BusinessAccountDto>> GetBusinessAccountsAsync(Guid fleetId, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        return await db.BusinessAccounts.AsNoTracking()
            .Where(b => b.FleetId == fleetId)
            .OrderBy(b => b.CompanyName)
            .Select(b => new BusinessAccountDto(b.Id, b.FleetId, b.CompanyName, b.AccountCode, b.ContactEmail))
            .ToListAsync(ct);
    }

    public async Task<BusinessAccountDto> CreateBusinessAccountAsync(Guid fleetId, CreateBusinessAccountRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var account = new BusinessAccount
        {
            Id = Guid.NewGuid(),
            FleetId = fleetId,
            CompanyName = request.CompanyName,
            AccountCode = request.AccountCode,
            ContactEmail = request.ContactEmail
        };
        db.BusinessAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return new BusinessAccountDto(account.Id, account.FleetId, account.CompanyName, account.AccountCode, account.ContactEmail);
    }

    public async Task<IReadOnlyList<DeliveryOrderDto>> GetOrdersAsync(Guid fleetId, DeliveryChannel? channel, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var query = ScopedOrders()
            .AsNoTracking()
            .Where(o => o.FleetId == fleetId);
        if (channel.HasValue)
            query = query.Where(o => o.Channel == channel.Value);

        var orders = await query
            .Include(o => o.BusinessAccount)
            .Include(o => o.Assignment)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        var vehicleIds = orders.Where(o => o.Assignment != null).Select(o => o.Assignment!.VehicleId).Distinct().ToList();
        var plates = await db.Vehicles.AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.LicensePlate, ct);

        return orders.Select(o => MapOrder(o, o.Assignment != null ? plates.GetValueOrDefault(o.Assignment.VehicleId) : null)).ToList();
    }

    public async Task<DeliveryOrderDto?> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await ScopedOrders()
            .AsNoTracking()
            .Include(o => o.BusinessAccount)
            .Include(o => o.Assignment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return null;

        string? plate = null;
        if (order.Assignment != null)
            plate = await db.Vehicles.AsNoTracking()
                .Where(v => v.Id == order.Assignment.VehicleId)
                .Select(v => v.LicensePlate)
                .FirstOrDefaultAsync(ct);

        return MapOrder(order, plate);
    }

    public async Task<DeliveryOrderDto> CreateOrderAsync(Guid fleetId, CreateDeliveryOrderRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        if (request.BusinessAccountId.HasValue)
        {
            var exists = await db.BusinessAccounts.AsNoTracking()
                .AnyAsync(b => b.Id == request.BusinessAccountId.Value && b.FleetId == fleetId, ct);
            if (!exists)
                throw new InvalidOperationException("Business account not found.");
        }

        var order = new DeliveryOrder
        {
            Id = Guid.NewGuid(),
            FleetId = fleetId,
            Channel = request.Channel,
            Status = DeliveryOrderStatus.Created,
            PickupAddress = request.PickupAddress,
            DeliveryAddress = request.DeliveryAddress,
            RecipientName = request.RecipientName,
            RecipientPhone = request.RecipientPhone,
            BusinessAccountId = request.BusinessAccountId,
            ParcelDescription = request.ParcelDescription,
            CreatedAt = DateTime.UtcNow
        };
        db.DeliveryOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return await GetOrderAsync(order.Id, ct) ?? MapOrder(order, null);
    }

    public async Task<DeliveryOrderDto> UpdateStatusAsync(Guid orderId, UpdateDeliveryStatusRequest request, CancellationToken ct = default)
    {
        var order = await ScopedOrders()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException("Order not found.");

        order.Status = request.Status;
        await db.SaveChangesAsync(ct);
        return (await GetOrderAsync(orderId, ct))!;
    }

    public async Task<DeliveryOrderDto> AssignAsync(Guid orderId, AssignDeliveryRequest request, CancellationToken ct = default)
    {
        var order = await ScopedOrders()
            .Include(o => o.Assignment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException("Order not found.");

        await ValidateVehicleForAssignmentAsync(order.FleetId, request.VehicleId, request.AutomationMode, ct);

        if (order.Assignment is null)
        {
            order.Assignment = new DeliveryAssignment
            {
                Id = Guid.NewGuid(),
                DeliveryOrderId = order.Id,
                VehicleId = request.VehicleId,
                AutomationMode = request.AutomationMode,
                AssignedAt = DateTime.UtcNow
            };
            db.DeliveryAssignments.Add(order.Assignment);
        }
        else
        {
            order.Assignment.VehicleId = request.VehicleId;
            order.Assignment.AutomationMode = request.AutomationMode;
            order.Assignment.AssignedAt = DateTime.UtcNow;
        }

        if (order.Status == DeliveryOrderStatus.Created)
            order.Status = DeliveryOrderStatus.Assigned;

        await db.SaveChangesAsync(ct);
        return (await GetOrderAsync(orderId, ct))!;
    }

    public async Task<IReadOnlyList<DeliveryVehicleDto>> GetVehiclesAsync(Guid fleetId, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.FleetId == fleetId).ToListAsync(ct);
        var profiles = await db.RoboTaxiProfiles.AsNoTracking()
            .Where(p => vehicles.Select(v => v.Id).Contains(p.VehicleId))
            .ToDictionaryAsync(p => p.VehicleId, ct);
        var locs = await LocationHelper.GetLatestForVehiclesAsync(db, vehicles.Select(v => v.Id), ct);

        return vehicles.Select(v =>
        {
            profiles.TryGetValue(v.Id, out var profile);
            return new DeliveryVehicleDto(
                v.Id, v.LicensePlate, v.Make, v.Model,
                profile != null, profile?.OperationalState, locs.GetValueOrDefault(v.Id));
        }).ToList();
    }

    public async Task<IReadOnlyList<DeliveryTrackingDto>> GetTrackingAsync(Guid fleetId, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.FleetId == fleetId).ToListAsync(ct);
        var profiles = await db.RoboTaxiProfiles.AsNoTracking()
            .Where(p => vehicles.Select(v => v.Id).Contains(p.VehicleId))
            .ToDictionaryAsync(p => p.VehicleId, ct);
        var locs = await LocationHelper.GetLatestForVehiclesAsync(db, vehicles.Select(v => v.Id), ct);

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
                v.Id, v.LicensePlate, profile != null, profile?.OperationalState, locs.GetValueOrDefault(v.Id),
                active?.Id, active?.Status, active?.DeliveryAddress);
        }).ToList();
    }

    public async Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesAsync(Guid fleetId, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        var routes = await ScopedRoutes()
            .AsNoTracking()
            .Where(r => r.FleetId == fleetId)
            .Include(r => r.Stops)
            .OrderByDescending(r => r.ScheduledDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var vehicleIds = routes.Where(r => r.VehicleId.HasValue).Select(r => r.VehicleId!.Value).Distinct().ToList();
        var plates = await db.Vehicles.AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.LicensePlate, ct);

        return routes.Select(r => MapRouteSummary(r, r.VehicleId.HasValue ? plates.GetValueOrDefault(r.VehicleId.Value) : null)).ToList();
    }

    public async Task<DeliveryRouteDetailDto?> GetRouteAsync(Guid routeId, CancellationToken ct = default)
    {
        var route = await ScopedRoutes()
            .AsNoTracking()
            .Include(r => r.Stops.OrderBy(s => s.Sequence))
            .FirstOrDefaultAsync(r => r.Id == routeId, ct);
        if (route is null) return null;

        string? plate = null;
        if (route.VehicleId.HasValue)
            plate = await db.Vehicles.AsNoTracking()
                .Where(v => v.Id == route.VehicleId.Value)
                .Select(v => v.LicensePlate)
                .FirstOrDefaultAsync(ct);

        return MapRouteDetail(route, plate);
    }

    public async Task<DeliveryRouteDetailDto> CreateRouteAsync(Guid fleetId, CreateDeliveryRouteRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureFleetAccess(fleetId);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Route name is required.");
        if (request.Stops.Count == 0)
            throw new ArgumentException("At least one stop is required.");
        if (string.IsNullOrWhiteSpace(request.DepotAddress))
            throw new ArgumentException("Depot address is required.");

        var routeId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var route = new DeliveryRoute
        {
            Id = routeId,
            FleetId = fleetId,
            Name = request.Name.Trim(),
            Status = DeliveryRouteStatus.Draft,
            DepotAddress = request.DepotAddress.Trim(),
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
                    Address = request.DepotAddress.Trim()
                }
            ]
        };

        var seq = 1;
        foreach (var stop in request.Stops)
        {
            if (string.IsNullOrWhiteSpace(stop.Address))
                throw new ArgumentException("Each stop requires a non-empty address.");
            route.Stops.Add(new DeliveryRouteStop
            {
                Id = Guid.NewGuid(),
                RouteId = routeId,
                Sequence = seq++,
                StopType = stop.StopType,
                Status = DeliveryStopStatus.Pending,
                Address = stop.Address.Trim(),
                RecipientName = stop.RecipientName,
                RecipientPhone = stop.RecipientPhone,
                ParcelDescription = stop.ParcelDescription,
                Notes = stop.Notes
            });
        }

        db.DeliveryRoutes.Add(route);
        await db.SaveChangesAsync(ct);
        return (await GetRouteAsync(routeId, ct))!;
    }

    public async Task<DeliveryRouteDetailDto> UpdateRouteAsync(Guid routeId, UpdateDeliveryRouteRequest request, CancellationToken ct = default)
    {
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        EnsureRouteEditable(route);

        if (string.IsNullOrWhiteSpace(request.DepotAddress))
            throw new ArgumentException("Depot address is required.");

        route.Name = request.Name.Trim();
        route.DepotAddress = request.DepotAddress.Trim();
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
                route.StartedAt ??= DateTime.UtcNow;
                var depot = route.Stops.FirstOrDefault(s => s.StopType == DeliveryStopType.Depot);
                if (depot is { Status: DeliveryStopStatus.Pending })
                {
                    depot.Status = DeliveryStopStatus.Completed;
                    depot.CompletedAt = DateTime.UtcNow;
                }
                break;
            case DeliveryRouteStatus.Completed when route.Status == DeliveryRouteStatus.InProgress:
                if (deliveryStops.Any(s => s.Status == DeliveryStopStatus.Pending))
                    throw new InvalidOperationException("All stops must be completed or skipped before completing the route.");
                route.CompletedAt = DateTime.UtcNow;
                break;
            case DeliveryRouteStatus.Cancelled when route.Status is not DeliveryRouteStatus.Completed:
                break;
            default:
                throw new InvalidOperationException($"Cannot transition route from {route.Status} to {request.Status}.");
        }

        route.Status = request.Status;
        await db.SaveChangesAsync(ct);
        return (await GetRouteAsync(routeId, ct))!;
    }

    public async Task<DeliveryRouteDetailDto> AssignRouteAsync(Guid routeId, AssignRouteRequest request, CancellationToken ct = default)
    {
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        EnsureRouteEditable(route);
        await ValidateVehicleForAssignmentAsync(route.FleetId, request.VehicleId, request.AutomationMode, ct);

        route.VehicleId = request.VehicleId;
        route.AutomationMode = request.AutomationMode;
        await db.SaveChangesAsync(ct);
        return (await GetRouteAsync(routeId, ct))!;
    }

    public async Task<DeliveryRouteStopDto> AddRouteStopAsync(Guid routeId, AddRouteStopRequest request, CancellationToken ct = default)
    {
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

        var seq = 1;
        foreach (var id in request.StopIds)
            stopById[id].Sequence = seq++;

        await db.SaveChangesAsync(ct);
        return (await GetRouteAsync(routeId, ct))!;
    }

    public async Task DeleteRouteStopAsync(Guid routeId, Guid stopId, CancellationToken ct = default)
    {
        var route = await LoadRouteForUpdateAsync(routeId, ct);
        EnsureRouteEditable(route);
        var stop = route.Stops.FirstOrDefault(s => s.Id == stopId)
            ?? throw new InvalidOperationException("Stop not found.");
        if (stop.StopType == DeliveryStopType.Depot)
            throw new InvalidOperationException("Depot stop cannot be deleted.");

        route.Stops.Remove(stop);
        db.DeliveryRouteStops.Remove(stop);

        var seq = 1;
        foreach (var remaining in route.Stops.Where(s => s.StopType != DeliveryStopType.Depot).OrderBy(s => s.Sequence))
            remaining.Sequence = seq++;

        await db.SaveChangesAsync(ct);
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
        await db.SaveChangesAsync(ct);
        return MapStop(stop);
    }

    private IQueryable<DeliveryOrder> ScopedOrders()
    {
        var query = db.DeliveryOrders.AsQueryable();
        if (!currentUser.IsPlatformAdmin && currentUser.FleetId.HasValue)
            query = query.Where(o => o.FleetId == currentUser.FleetId.Value);
        return query;
    }

    private IQueryable<DeliveryRoute> ScopedRoutes()
    {
        var query = db.DeliveryRoutes.AsQueryable();
        if (!currentUser.IsPlatformAdmin && currentUser.FleetId.HasValue)
            query = query.Where(r => r.FleetId == currentUser.FleetId.Value);
        return query;
    }

    private async Task<List<Guid>> ScopedDeliveryFleetIdsAsync(CancellationToken ct)
    {
        var query = db.Fleets.AsNoTracking().Where(f => f.FleetType == FleetType.Delivery);
        if (!currentUser.IsPlatformAdmin && currentUser.FleetId.HasValue)
            query = query.Where(f => f.Id == currentUser.FleetId.Value);
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

    private async Task ValidateVehicleForAssignmentAsync(Guid fleetId, Guid vehicleId, AutomationMode mode, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehicle not found.");
        if (vehicle.FleetId != fleetId)
            throw new InvalidOperationException("Vehicle does not belong to this fleet.");

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

    private static DeliveryOrderDto MapOrder(DeliveryOrder order, string? licensePlate) =>
        new(
            order.Id,
            order.FleetId,
            order.Channel,
            order.Status,
            order.PickupAddress,
            order.DeliveryAddress,
            order.RecipientName,
            order.RecipientPhone,
            order.BusinessAccountId,
            order.BusinessAccount?.CompanyName,
            order.ParcelDescription,
            order.Assignment is null ? null : new DeliveryAssignmentDto(
                order.Assignment.Id,
                order.Assignment.VehicleId,
                licensePlate ?? string.Empty,
                order.Assignment.AutomationMode,
                order.Assignment.AssignedAt),
            order.CreatedAt);

    private static DeliveryRouteDto MapRouteSummary(DeliveryRoute route, string? licensePlate)
    {
        var (completed, pending) = CountDeliveryStopProgress(route.Stops);
        return new DeliveryRouteDto(
            route.Id,
            route.FleetId,
            route.Name,
            route.Status,
            route.DepotAddress,
            route.ScheduledDate,
            route.VehicleId,
            licensePlate,
            route.AutomationMode,
            route.Stops.Count(s => s.StopType != DeliveryStopType.Depot),
            completed,
            pending,
            route.CreatedAt,
            route.StartedAt,
            route.CompletedAt);
    }

    private static DeliveryRouteDetailDto MapRouteDetail(DeliveryRoute route, string? licensePlate)
    {
        var stops = route.Stops.OrderBy(s => s.Sequence).Select(MapStop).ToList();
        return new DeliveryRouteDetailDto(
            route.Id,
            route.FleetId,
            route.Name,
            route.Status,
            route.DepotAddress,
            route.ScheduledDate,
            route.VehicleId,
            licensePlate,
            route.AutomationMode,
            stops,
            route.CreatedAt,
            route.StartedAt,
            route.CompletedAt);
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
            stop.CompletedAt);

    private static (int Completed, int Pending) CountDeliveryStopProgress(IEnumerable<DeliveryRouteStop> stops)
    {
        var delivery = stops.Where(s => s.StopType != DeliveryStopType.Depot);
        return (
            delivery.Count(s => s.Status == DeliveryStopStatus.Completed),
            delivery.Count(s => s.Status == DeliveryStopStatus.Pending));
    }
}
