using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Entities;
using UniConnect.Delivery.Enums;
using UniConnect.Delivery.Interfaces;
using UniConnect.GeneralFleet.Enums;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Enums;
using UniConnect.Insights.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.RoutePlanning.DTOs;
using UniConnect.RoutePlanning.Entities;
using UniConnect.RoutePlanning.Enums;
using Microsoft.Extensions.Options;
using UniConnect.RoutePlanning.Interfaces;
using UniConnect.RoutePlanning.Models;
using UniConnect.RoutePlanning.Options;
using UniConnect.RoutePlanning.Routing;
using UniConnect.Infrastructure.Services.RoutePlanning;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services;

public class RoutePlanningService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IOperationalEventRecorder events,
    IDeliveryService deliveryService,
    IGeocodingService geocoding,
    ITravelTimeMatrix travelTimeMatrix,
    OrderGeocodingHelper orderGeocoding,
    IDepotDirectory depots,
    IOptions<RoutePlanningOptions> planningOptions) : IRoutePlanningService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<RoutePlanRunDto> PlanRoutesAsync(Guid tenantId, PlanRoutesRequest request, CancellationToken ct = default)
    {
        EnsureRoutePlanningAccess(tenantId);
        var userId = currentUser.UserId ?? throw new InvalidOperationException("Authenticated user required.");
        var plannerLabel = currentUser.DisplayName ?? currentUser.Email ?? "Planner";

        var depotAddress = await depots.ResolveDepotAddressAsync(tenantId, request.DepotId, request.DepotAddress, ct);

        var planRun = new RoutePlanRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequestedByUserId = userId,
            Status = RoutePlanRunStatus.Requested,
            ScheduledDate = request.ScheduledDate,
            DepotAddress = depotAddress,
            DepotId = request.DepotId,
            CreatedAt = DateTime.UtcNow
        };
        db.RoutePlanRuns.Add(planRun);
        await db.SaveChangesAsync(ct);

        await events.RecordAsync(new RecordOperationalEventRequest(
            tenantId,
            InsightsDomains.RoutePlanning,
            RoutePlanningEventTypes.PlanRequested,
            UserId: userId,
            PlanRunId: planRun.Id,
            PlannerLabel: plannerLabel,
            Narrative: $"Plan requested for {request.ScheduledDate:yyyy-MM-dd} from depot {planRun.DepotAddress}.",
            Metrics: new Dictionary<string, object?> { ["scheduledDate"] = request.ScheduledDate.ToString("O") }),
            ct);

        var sw = Stopwatch.StartNew();
        var orders = await LoadOrdersForPlanningAsync(tenantId, request.OrderIds, ct);
        planRun.OrdersRequested = orders.Count;

        await orderGeocoding.EnsureOrdersCoordinatesAsync(orders, ct);

        var vehicles = await LoadVehiclesForPlanningAsync(tenantId, request.DepotId, request.VehicleIds, ct);
        if (vehicles.Count == 0)
        {
            if (request.DepotId.HasValue)
                throw new ArgumentException("No active vehicles are assigned to the selected depot.");
            throw new ArgumentException("At least one vehicle is required for planning.");
        }

        var depotPoint = await geocoding.GeocodeAsync(planRun.DepotAddress, ct)
            ?? throw new InvalidOperationException("Unable to geocode depot address.");

        var plannable = BuildPlannableOrders(orders);
        var partitions = PickupDeliveryRouteOptimizer.PartitionOrdersAcrossVehicles(
            depotPoint,
            planRun.DepotAddress,
            plannable,
            vehicles.Count,
            request.MaxStopsPerRoute,
            travelTimeMatrix);

        var proposals = BuildProposals(partitions, vehicles, depotPoint, planRun.DepotAddress);
        planRun.ProposalCount = proposals.Count;
        planRun.OrdersPlanned = partitions.Sum(p => p.Count);
        planRun.OrdersUnassigned = Math.Max(0, orders.Count - planRun.OrdersPlanned);
        planRun.ProposalJson = JsonSerializer.Serialize(proposals, JsonOptions);
        planRun.Status = RoutePlanRunStatus.Completed;
        planRun.CompletedAt = DateTime.UtcNow;
        planRun.ComputeDurationMs = sw.ElapsedMilliseconds;
        await db.SaveChangesAsync(ct);

        await events.RecordAsync(new RecordOperationalEventRequest(
            tenantId,
            InsightsDomains.RoutePlanning,
            RoutePlanningEventTypes.PlanCompleted,
            UserId: userId,
            PlanRunId: planRun.Id,
            PlannerLabel: plannerLabel,
            Narrative: $"Plan completed with {planRun.ProposalCount} route proposal(s); {planRun.OrdersUnassigned} order(s) unassigned.",
            Metrics: new Dictionary<string, object?>
            {
                ["ordersRequested"] = planRun.OrdersRequested,
                ["ordersPlanned"] = planRun.OrdersPlanned,
                ["ordersUnassigned"] = planRun.OrdersUnassigned,
                ["proposalCount"] = planRun.ProposalCount,
                ["computeDurationMs"] = planRun.ComputeDurationMs
            }),
            ct);

        return await MapPlanRunAsync(planRun, ct);
    }

    public async Task<PlanReadinessDto> GetPlanReadinessAsync(Guid tenantId, Guid? depotId = null, CancellationToken ct = default)
    {
        EnsureRoutePlanningAccess(tenantId);

        var orders = await LoadOrdersForPlanningAsync(tenantId, null, ct);
        await orderGeocoding.EnsureOrdersCoordinatesAsync(orders, ct);
        var plannable = BuildPlannableOrders(orders);

        var allVehicles = await db.Vehicles.AsNoTracking()
            .Where(v => v.TenantId == tenantId)
            .ToListAsync(ct);
        var scopedVehicles = depotId.HasValue
            ? allVehicles.Where(v => v.HomeDepotId == depotId.Value).ToList()
            : allVehicles;
        var activeVehicleCount = scopedVehicles.Count(v => v.Status == VehicleStatus.Active);
        var unassignedCount = allVehicles.Count(v => !v.HomeDepotId.HasValue);

        var notes = new List<string>();
        if (orders.Count == 0)
            notes.Add("No orders in Ready status. Only orders marked Ready are included in planning.");
        else if (plannable.Count < orders.Count)
            notes.Add($"{orders.Count - plannable.Count} ready order(s) could not be geocoded. Check pickup and delivery addresses.");

        if (allVehicles.Count == 0)
            notes.Add("No vehicles are registered to this delivery fleet. Add vehicles under Fleet → Vehicles.");
        else if (depotId.HasValue && scopedVehicles.Count == 0)
            notes.Add("No vehicles are assigned to this depot. Assign a home depot on each vehicle under Fleet → Vehicles.");
        else if (activeVehicleCount == 0)
            notes.Add(depotId.HasValue
                ? "Vehicles are assigned to this depot but none are Active. Mark a vehicle Active under Fleet → Vehicles."
                : "Vehicles exist but none are Active. Mark a vehicle Active under Fleet → Vehicles.");
        else
            notes.Add(depotId.HasValue
                ? $"Planning uses {activeVehicleCount} active vehicle(s) assigned to this depot."
                : $"Planning uses all {activeVehicleCount} active vehicle(s) in this fleet.");

        if (unassignedCount > 0 && !depotId.HasValue)
            notes.Add($"{unassignedCount} vehicle(s) have no home depot assigned — assign depots under Fleet → Vehicles for accurate multi-depot planning.");

        var canPlan = orders.Count > 0 && plannable.Count > 0 && activeVehicleCount > 0;
        return new PlanReadinessDto(
            orders.Count,
            plannable.Count,
            scopedVehicles.Count,
            activeVehicleCount,
            canPlan,
            notes);
    }

    public async Task<RoutePlanRunDto?> GetPlanRunAsync(Guid planRunId, CancellationToken ct = default)
    {
        var planRun = await ScopedPlanRuns().AsNoTracking().FirstOrDefaultAsync(p => p.Id == planRunId, ct);
        return planRun is null ? null : await MapPlanRunAsync(planRun, ct);
    }

    public async Task<IReadOnlyList<RoutePlanRunDto>> GetPlanRunsAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureRoutePlanningAccess(tenantId);
        var runs = await ScopedPlanRuns().AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var mapped = new List<RoutePlanRunDto>();
        foreach (var run in runs)
            mapped.Add(await MapPlanRunAsync(run, ct));
        return mapped;
    }

    public async Task<AcceptPlanResultDto> AcceptPlanAsync(Guid planRunId, AcceptPlanRequest request, CancellationToken ct = default)
    {
        var planRun = await ScopedPlanRuns().FirstOrDefaultAsync(p => p.Id == planRunId, ct)
            ?? throw new InvalidOperationException("Plan run not found.");
        EnsureRoutePlanningAccess(planRun.TenantId);

        if (planRun.Status != RoutePlanRunStatus.Completed)
            throw new InvalidOperationException("Only completed plans can be accepted.");

        var proposals = DeserializeProposals(planRun.ProposalJson);
        proposals = await RefreshProposalsFromOrdersAsync(proposals, ct);
        var depotPoint = await geocoding.GeocodeAsync(planRun.DepotAddress, ct);
        if (depotPoint.HasValue)
            proposals = FilterDepotPickupStops(proposals, planRun.DepotAddress, depotPoint.Value);
        var selected = request.ProposalVehicleIds is { Count: > 0 }
            ? proposals.Where(p => p.VehicleId.HasValue && request.ProposalVehicleIds.Contains(p.VehicleId.Value)).ToList()
            : proposals;

        if (selected.Count == 0)
            throw new ArgumentException("Select at least one proposal to accept.");

        var plannedOrderIds = selected
            .SelectMany(p => p.Stops)
            .Where(s => s.OrderId.HasValue)
            .Select(s => s.OrderId!.Value)
            .Distinct()
            .ToList();
        var ordersById = plannedOrderIds.Count == 0
            ? new Dictionary<Guid, DeliveryOrder>()
            : await db.DeliveryOrders
                .Where(o => o.TenantId == planRun.TenantId && plannedOrderIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, ct);

        var routeIndex = 1;
        var createdRoutes = new List<AcceptedRouteSummaryDto>();
        foreach (var proposal in selected)
        {
            var deliveryStops = proposal.Stops
                .Where(s => s.StopType is "Pickup" or "Dropoff")
                .Select(s =>
                {
                    string? parcel = s.ParcelDescription;
                    if (string.IsNullOrWhiteSpace(parcel)
                        && s.OrderId.HasValue
                        && ordersById.TryGetValue(s.OrderId.Value, out var order))
                        parcel = order.ParcelDescription;

                    return new CreateRouteStopRequest(
                        s.StopType == "Pickup" ? DeliveryStopType.Pickup : DeliveryStopType.Dropoff,
                        s.Address,
                        s.RecipientName,
                        null,
                        parcel,
                        null,
                        s.OrderId);
                })
                .ToList();

            var routeName = $"Plan {planRun.ScheduledDate:yyyy-MM-dd} #{routeIndex++}";
            var created = await deliveryService.CreateRouteAsync(
                planRun.TenantId,
                new CreateDeliveryRouteRequest(
                    routeName,
                    planRun.ScheduledDate,
                    deliveryStops,
                    planRun.DepotAddress,
                    planRun.DepotId),
                ct);

            var route = await db.DeliveryRoutes.FirstAsync(r => r.Id == created.Id, ct);
            route.RoutePlanRunId = planRun.Id;
            if (proposal.VehicleId.HasValue)
            {
                route.VehicleId = proposal.VehicleId;
                route.AutomationMode = AutomationMode.Conventional;
            }
            await db.SaveChangesAsync(ct);
            if (proposal.VehicleId.HasValue)
                await deliveryService.SyncRouteOrderAssignmentsAsync(created.Id, ct);
            createdRoutes.Add(new AcceptedRouteSummaryDto(created.Id, routeName));
        }

        if (ordersById.Count > 0)
        {
            foreach (var order in ordersById.Values.Where(o => o.Status == DeliveryOrderStatus.Created))
                order.Status = DeliveryOrderStatus.Assigned;
            await db.SaveChangesAsync(ct);
        }

        planRun.Status = RoutePlanRunStatus.Accepted;
        planRun.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var userId = currentUser.UserId;
        await events.RecordAsync(new RecordOperationalEventRequest(
            planRun.TenantId,
            InsightsDomains.RoutePlanning,
            RoutePlanningEventTypes.PlanAccepted,
            UserId: userId,
            PlanRunId: planRun.Id,
            PlannerLabel: currentUser.DisplayName,
            Narrative: $"Accepted plan with {selected.Count} draft route(s).",
            Metrics: new Dictionary<string, object?> { ["routesCreated"] = selected.Count }),
            ct);

        return new AcceptPlanResultDto(
            (await MapPlanRunAsync(planRun, ct))!,
            createdRoutes);
    }

    public async Task<RoutePlanRunDto> DiscardPlanAsync(Guid planRunId, DiscardPlanRequest request, CancellationToken ct = default)
    {
        var planRun = await ScopedPlanRuns().FirstOrDefaultAsync(p => p.Id == planRunId, ct)
            ?? throw new InvalidOperationException("Plan run not found.");
        EnsureRoutePlanningAccess(planRun.TenantId);

        if (planRun.Status is RoutePlanRunStatus.Accepted or RoutePlanRunStatus.Discarded)
            throw new InvalidOperationException("This plan run is already resolved.");

        planRun.Status = RoutePlanRunStatus.Discarded;
        planRun.DiscardReason = request.Reason?.Trim();
        planRun.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await events.RecordAsync(new RecordOperationalEventRequest(
            planRun.TenantId,
            InsightsDomains.RoutePlanning,
            RoutePlanningEventTypes.PlanDiscarded,
            UserId: currentUser.UserId,
            PlanRunId: planRun.Id,
            PlannerLabel: currentUser.DisplayName,
            Narrative: string.IsNullOrWhiteSpace(planRun.DiscardReason)
                ? "Plan discarded."
                : $"Plan discarded: {planRun.DiscardReason}"),
            ct);

        return (await MapPlanRunAsync(planRun, ct))!;
    }

    public async Task<OptimizeSequenceResultDto> OptimizeRouteSequenceAsync(Guid routeId, OptimizeSequenceRequest request, CancellationToken ct = default)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        var route = await db.DeliveryRoutes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == routeId, ct)
            ?? throw new InvalidOperationException("Route not found.");
        EnsureRoutePlanningAccess(route.TenantId);

        var deliveryStops = route.Stops
            .Where(s => s.StopType != DeliveryStopType.Depot)
            .OrderBy(s => s.Sequence)
            .ToList();

        var geocoded = await geocoding.GeocodeManyAsync(
            deliveryStops.Select(s => s.Address).Append(route.DepotAddress),
            ct);
        if (!geocoded.TryGetValue(route.DepotAddress, out var depotPoint))
            throw new InvalidOperationException("Unable to geocode depot address.");

        var operationalStops = deliveryStops
            .Where(s => !IsDepotPickupRouteStop(s, route.DepotAddress, depotPoint))
            .ToList();
        if (request.StopIds.Count != operationalStops.Count || request.StopIds.Distinct().Count() != request.StopIds.Count)
            throw new ArgumentException("Provide all delivery stop IDs exactly once.");

        var pickedUpAtDepot = deliveryStops
            .Where(s => s.StopType == DeliveryStopType.Pickup && IsDepotPickupRouteStop(s, route.DepotAddress, depotPoint))
            .Where(s => s.DeliveryOrderId.HasValue)
            .Select(s => s.DeliveryOrderId!.Value)
            .ToHashSet();

        var linkedOrderIds = operationalStops
            .Where(s => s.DeliveryOrderId.HasValue)
            .Select(s => s.DeliveryOrderId!.Value)
            .Distinct()
            .ToList();
        if (linkedOrderIds.Count > 0)
        {
            var orders = await db.DeliveryOrders.AsNoTracking()
                .Where(o => linkedOrderIds.Contains(o.Id))
                .ToListAsync(ct);
            foreach (var order in orders)
            {
                GeoPoint? pickup = order.PickupLatitude is decimal lat && order.PickupLongitude is decimal lng
                    ? new GeoPoint(lat, lng)
                    : null;
                if (DepotPickupMatcher.IsPickupAtDepot(order.PickupAddress, pickup, depotPoint, route.DepotAddress))
                    pickedUpAtDepot.Add(order.Id);
            }
        }

        var stopById = operationalStops.ToDictionary(s => s.Id);
        var requestedStops = request.StopIds.Select(id => stopById[id]).ToList();
        var planningStops = new List<PlanningStop>();
        foreach (var stop in requestedStops)
        {
            if (!geocoded.TryGetValue(stop.Address, out var location))
                throw new InvalidOperationException($"Unable to geocode stop address: {stop.Address}");

            planningStops.Add(new PlanningStop(
                stop.DeliveryOrderId,
                stop.StopType == DeliveryStopType.Pickup ? "Pickup" : "Dropoff",
                stop.Address,
                location,
                stop.RecipientName,
                stop.ParcelDescription,
                stop.Id));
        }

        var serviceMinutes = planningOptions.Value.DefaultServiceMinutes;
        var beforeMinutes = PickupDeliveryRouteOptimizer.EstimateRouteMinutes(
            depotPoint, planningStops, travelTimeMatrix, serviceMinutes);

        var optimizedNodes = PickupDeliveryRouteOptimizer.OptimizeStopSequence(
            depotPoint,
            planningStops,
            travelTimeMatrix,
            pickedUpAtDepot);
        var afterMinutes = PickupDeliveryRouteOptimizer.EstimateRouteMinutes(
            depotPoint, optimizedNodes, travelTimeMatrix, serviceMinutes);

        var optimized = optimizedNodes.Select(n => stopById[n.RouteStopId!.Value]).ToList();

        var depotPickupStops = deliveryStops
            .Where(s => IsDepotPickupRouteStop(s, route.DepotAddress, depotPoint))
            .ToList();
        if (depotPickupStops.Count > 0)
            db.DeliveryRouteStops.RemoveRange(depotPickupStops);

        var tempSeq = -1;
        foreach (var stop in optimized)
            stop.Sequence = tempSeq--;
        await db.SaveChangesAsync(ct);

        var seq = 1;
        foreach (var stop in optimized)
            stop.Sequence = seq++;
        await db.SaveChangesAsync(ct);

        await events.RecordAsync(new RecordOperationalEventRequest(
            route.TenantId,
            InsightsDomains.RoutePlanning,
            RoutePlanningEventTypes.SequenceOptimized,
            RouteId: route.Id,
            UserId: currentUser.UserId,
            PlannerLabel: currentUser.DisplayName,
            Narrative: $"Optimized stop sequence on route {route.Name}.",
            Metrics: new Dictionary<string, object?>
            {
                ["estimatedMinutesBefore"] = beforeMinutes,
                ["estimatedMinutesAfter"] = afterMinutes
            }),
            ct);

        return new OptimizeSequenceResultDto(
            route.Id,
            optimized.Select(s => s.Id).ToList(),
            beforeMinutes,
            afterMinutes);
    }

    private async Task<List<DeliveryOrder>> LoadOrdersForPlanningAsync(Guid tenantId, IReadOnlyList<Guid>? orderIds, CancellationToken ct)
    {
        var query = db.DeliveryOrders
            .Where(o => o.TenantId == tenantId && o.Status == DeliveryOrderStatus.Created);

        if (orderIds is { Count: > 0 })
            query = query.Where(o => orderIds.Contains(o.Id));

        return await query.OrderBy(o => o.CreatedAt).ToListAsync(ct);
    }

    private async Task<List<(Guid Id, string Label)>> LoadVehiclesForPlanningAsync(
        Guid tenantId,
        Guid? depotId,
        IReadOnlyList<Guid>? vehicleIds,
        CancellationToken ct)
    {
        var query = db.Vehicles.AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.Status == VehicleStatus.Active);
        if (depotId.HasValue)
            query = query.Where(v => v.HomeDepotId == depotId.Value);
        if (vehicleIds is { Count: > 0 })
            query = query.Where(v => vehicleIds.Contains(v.Id));

        return await query.OrderBy(v => v.LicensePlate).Select(v => new ValueTuple<Guid, string>(v.Id, v.LicensePlate)).ToListAsync(ct);
    }

    private List<OrderForPlanning> BuildPlannableOrders(IReadOnlyList<DeliveryOrder> orders)
    {
        var plannable = new List<OrderForPlanning>();
        foreach (var order in orders)
        {
            var pickup = orderGeocoding.PickupPoint(order);
            var delivery = orderGeocoding.DeliveryPoint(order);
            if (pickup is null || delivery is null)
                continue;

            plannable.Add(new OrderForPlanning(
                order.Id,
                order.PickupAddress,
                order.DeliveryAddress,
                pickup.Value,
                delivery.Value,
                order.RecipientName,
                order.ParcelDescription));
        }

        return plannable;
    }

    private List<PlannedRouteProposalDto> BuildProposals(
        IReadOnlyList<IReadOnlyList<OrderForPlanning>> partitions,
        IReadOnlyList<(Guid Id, string Label)> vehicles,
        GeoPoint depot,
        string depotAddress)
    {
        var serviceMinutes = planningOptions.Value.DefaultServiceMinutes;
        var proposals = new List<PlannedRouteProposalDto>();

        for (var i = 0; i < partitions.Count; i++)
        {
            var vehicle = vehicles[i];
            var routeOrders = partitions[i];
            var rawStops = PickupDeliveryRouteOptimizer.BuildPlanningStops(routeOrders, depot, depotAddress);
            var pickedUpAtDepot = PickupDeliveryRouteOptimizer.PickedUpAtDepotOrderIds(routeOrders, depot, depotAddress);

            var optimized = PickupDeliveryRouteOptimizer.OptimizeStopSequence(
                depot,
                rawStops,
                travelTimeMatrix,
                pickedUpAtDepot);
            var sequence = 1;
            var plannedStops = optimized.Select(s => new PlannedStopDto(
                s.OrderId,
                s.StopType,
                sequence++,
                s.Address,
                s.RecipientName,
                s.Location.Latitude,
                s.Location.Longitude,
                s.ParcelDescription)).ToList();

            proposals.Add(new PlannedRouteProposalDto(
                vehicle.Id,
                vehicle.Label,
                PickupDeliveryRouteOptimizer.EstimateRouteMinutes(depot, optimized, travelTimeMatrix, serviceMinutes),
                plannedStops));
        }

        return proposals;
    }

    private static bool IsDepotPickupRouteStop(DeliveryRouteStop stop, string depotAddress, GeoPoint depot) =>
        stop.StopType == DeliveryStopType.Pickup
        && DepotPickupMatcher.IsPickupAtDepot(stop.Address, (GeoPoint?)null, depot, depotAddress);

    private static IReadOnlyList<PlannedRouteProposalDto> FilterDepotPickupStops(
        IReadOnlyList<PlannedRouteProposalDto> proposals,
        string depotAddress,
        GeoPoint depot) =>
        proposals
            .Select(proposal => proposal with
            {
                Stops = proposal.Stops
                    .Where(s => s.StopType != "Pickup"
                        || !DepotPickupMatcher.IsPickupAtDepot(
                            s.Address,
                            s.Latitude.HasValue && s.Longitude.HasValue
                                ? new GeoPoint(s.Latitude.Value, s.Longitude.Value)
                                : null,
                            depot,
                            depotAddress))
                    .ToList(),
            })
            .ToList();

    private async Task<RoutePlanRunDto> MapPlanRunAsync(RoutePlanRun planRun, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == planRun.RequestedByUserId, ct);
        decimal? depotLat = null;
        decimal? depotLng = null;
        if (!string.IsNullOrWhiteSpace(planRun.DepotAddress))
        {
            var depot = await geocoding.GeocodeAsync(planRun.DepotAddress, ct);
            if (depot.HasValue)
            {
                depotLat = depot.Value.Latitude;
                depotLng = depot.Value.Longitude;
            }
        }

        var proposals = await RefreshProposalsFromOrdersAsync(DeserializeProposals(planRun.ProposalJson), ct);
        if (depotLat.HasValue && depotLng.HasValue)
        {
            var depot = new GeoPoint(depotLat.Value, depotLng.Value);
            proposals = FilterDepotPickupStops(proposals, planRun.DepotAddress, depot);
            proposals = RecalculateProposalEstimates(proposals, depot);
        }

        return new RoutePlanRunDto(
            planRun.Id,
            planRun.TenantId,
            planRun.RequestedByUserId,
            user?.DisplayName ?? "Unknown",
            planRun.Status,
            planRun.ScheduledDate,
            planRun.DepotAddress,
            depotLat,
            depotLng,
            planRun.OrdersRequested,
            planRun.OrdersPlanned,
            planRun.OrdersUnassigned,
            planRun.ProposalCount,
            planRun.ComputeDurationMs,
            proposals,
            planRun.CreatedAt,
            planRun.CompletedAt,
            planRun.ResolvedAt);
    }

    private static IReadOnlyList<PlannedRouteProposalDto> DeserializeProposals(string? json) =>
        PlanProposalSync.Deserialize(json);

    private async Task<IReadOnlyList<PlannedRouteProposalDto>> RefreshProposalsFromOrdersAsync(
        IReadOnlyList<PlannedRouteProposalDto> proposals,
        CancellationToken ct)
    {
        if (proposals.Count == 0)
            return proposals;

        var orderIds = proposals
            .SelectMany(p => p.Stops)
            .Where(s => s.OrderId.HasValue)
            .Select(s => s.OrderId!.Value)
            .Distinct()
            .ToList();

        if (orderIds.Count == 0)
            return proposals;

        var orders = await db.DeliveryOrders.AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => ToOrderSnapshot(o), ct);

        return PlanProposalSync.RefreshFromOrders(proposals, orders);
    }

    private IReadOnlyList<PlannedRouteProposalDto> RecalculateProposalEstimates(
        IReadOnlyList<PlannedRouteProposalDto> proposals,
        GeoPoint depot)
    {
        var serviceMinutes = planningOptions.Value.DefaultServiceMinutes;
        return proposals
            .Select(proposal =>
            {
                var planningStops = proposal.Stops
                    .Where(s => s is { Latitude: not null, Longitude: not null }
                        && s.StopType is "Pickup" or "Dropoff")
                    .Select(s => new PlanningStop(
                        s.OrderId,
                        s.StopType,
                        s.Address,
                        new GeoPoint(s.Latitude!.Value, s.Longitude!.Value),
                        s.RecipientName,
                        s.ParcelDescription))
                    .ToList();

                if (planningStops.Count == 0)
                    return proposal;

                return proposal with
                {
                    EstimatedMinutes = PickupDeliveryRouteOptimizer.EstimateRouteMinutes(
                        depot,
                        planningStops,
                        travelTimeMatrix,
                        serviceMinutes),
                };
            })
            .ToList();
    }

    private static OrderStopSnapshot ToOrderSnapshot(DeliveryOrder order) =>
        new(
            order.Id,
            order.PickupAddress,
            order.DeliveryAddress,
            order.RecipientName,
            order.ParcelDescription,
            order.PickupLatitude,
            order.PickupLongitude,
            order.DeliveryLatitude,
            order.DeliveryLongitude);

    private IQueryable<RoutePlanRun> ScopedPlanRuns()
    {
        var query = db.RoutePlanRuns.AsQueryable();
        if (!currentUser.IsPlatformAdmin && currentUser.TenantId.HasValue)
            query = query.Where(p => p.TenantId == currentUser.TenantId.Value);
        return query;
    }

    private void EnsureRoutePlanningAccess(Guid tenantId)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.RoutePlanning);
        currentUser.EnsureTenantAccess(tenantId);
    }
}
