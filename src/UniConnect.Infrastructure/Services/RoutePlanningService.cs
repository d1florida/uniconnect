using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.Delivery;
using UniConnect.Delivery.DTOs;
using UniConnect.Delivery.Entities;
using UniConnect.Delivery.Enums;
using UniConnect.Delivery.Interfaces;
using UniConnect.GeneralFleet.Enums;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;
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
using UniConnect.Tenant.Interfaces;

namespace UniConnect.Infrastructure.Services;

public class RoutePlanningService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IOperationalEventRecorder events,
    IDeliveryService deliveryService,
    IGeocodingService geocoding,
    ITravelTimeMatrix travelTimeMatrix,
    ITravelMatrixBuilder travelMatrixBuilder,
    OrderGeocodingHelper orderGeocoding,
    IDepotDirectory depots,
    IPlanningPolicyProvider planningPolicyProvider,
    IPlanRunExplainer planRunExplainer,
    IDriverScheduleResolver driverScheduleResolver,
    ITenantDeliverySettingsService deliverySettings,
    IOptions<RoutePlanningOptions> planningOptions) : IRoutePlanningService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<RoutePlanRunDto> PlanRoutesAsync(Guid tenantId, PlanRoutesRequest request, CancellationToken ct = default)
    {
        EnsureRoutePlanningAccess(tenantId);
        var userId = currentUser.UserId ?? throw new InvalidOperationException("Authenticated user required.");
        var plannerLabel = currentUser.DisplayName ?? currentUser.Email ?? "Planner";

        FixedRouteTemplate? fixedTemplate = null;
        if (request.FixedRouteTemplateId.HasValue)
        {
            fixedTemplate = await db.FixedRouteTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.Id == request.FixedRouteTemplateId.Value
                    && t.TenantId == tenantId
                    && t.IsActive, ct)
                ?? throw new ArgumentException("Fixed route template not found.");

            if (!FixedRouteScheduleHelper.IncludesDayOfWeek(fixedTemplate.RouteDays, request.ScheduledDate.DayOfWeek))
            {
                throw new ArgumentException(
                    $"Scheduled date must fall on {FixedRouteScheduleHelper.FormatDaysOfWeek(fixedTemplate.RouteDays)} for fixed route {fixedTemplate.Name}.");
            }
        }

        var depotId = request.DepotId ?? fixedTemplate?.DepotId;
        var vehicleIds = request.VehicleIds;
        if ((vehicleIds is null || vehicleIds.Count == 0) && fixedTemplate?.DefaultVehicleId is Guid defaultVehicle)
            vehicleIds = [defaultVehicle];

        var driverIds = request.DriverIds;
        if ((driverIds is null || driverIds.Count == 0) && fixedTemplate?.DefaultDriverId is Guid defaultDriver)
            driverIds = [defaultDriver];

        var depotAddress = await depots.ResolveDepotAddressAsync(tenantId, depotId, request.DepotAddress, ct);
        var policy = await planningPolicyProvider.GetPolicyForTenantAsync(tenantId, ct);
        var maxStopsPerRoute = request.MaxStopsPerRoute > 0
            ? Math.Min(request.MaxStopsPerRoute, policy.Fleet.MaxStopsPerRoute)
            : policy.Fleet.MaxStopsPerRoute;

        var planRun = new RoutePlanRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequestedByUserId = userId,
            Status = RoutePlanRunStatus.Requested,
            ScheduledDate = request.ScheduledDate,
            DepotAddress = depotAddress,
            DepotId = depotId,
            FixedRouteTemplateId = fixedTemplate?.Id,
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
            Narrative: fixedTemplate is null
                ? $"Plan requested for {request.ScheduledDate:yyyy-MM-dd} from depot {planRun.DepotAddress}."
                : $"Fixed route plan requested for {fixedTemplate.Name} on {request.ScheduledDate:yyyy-MM-dd}.",
            Metrics: new Dictionary<string, object?>
            {
                ["scheduledDate"] = request.ScheduledDate.ToString("O"),
                ["fixedRouteTemplateId"] = fixedTemplate?.Id
            }),
            ct);

        var sw = Stopwatch.StartNew();
        var orders = await LoadOrdersForPlanningAsync(
            tenantId,
            request.OrderIds,
            request.FixedRouteTemplateId,
            request.ScheduledDate,
            ct);
        planRun.OrdersRequested = orders.Count;

        await orderGeocoding.EnsureOrdersCoordinatesAsync(orders, ct);

        var vehicles = await LoadVehiclesForPlanningAsync(tenantId, depotId, vehicleIds, ct);
        if (vehicles.Count == 0)
        {
            if (request.DepotId.HasValue)
                throw new ArgumentException("No active vehicles are assigned to the selected depot.");
            throw new ArgumentException("At least one vehicle is required for planning.");
        }

        var driverEntities = await LoadDriverEntitiesForPlanningAsync(tenantId, driverIds, ct);
        var resolvedByDriver = await driverScheduleResolver.ResolveAsync(
            tenantId,
            request.ScheduledDate,
            driverEntities.Select(d => d.Id).ToList(),
            ct);
        var tenantDelivery = await deliverySettings.GetSettingsAsync(tenantId, ct);
        var allowMultipleRoutesPerDriver = tenantDelivery.AllowMultipleRoutesPerDriverPerDay;
        var workingDrivers = driverEntities
            .Where(d => resolvedByDriver.TryGetValue(d.Id, out var day) && day.IsWorking)
            .ToList();
        if (fixedTemplate is null)
        {
            var unavailable = await GetDriversUnavailableForDailyPlanningAsync(
                tenantId, request.ScheduledDate, allowMultipleRoutesPerDriver, ct);
            if (unavailable.Count > 0)
                workingDrivers = workingDrivers.Where(d => !unavailable.Contains(d.Id)).ToList();
        }

        var drivers = BuildDriverPlanningSlots(workingDrivers, resolvedByDriver);
        var serviceMinutes = planningOptions.Value.DefaultServiceMinutes;
        var defaultShiftMinutes = DriverSchedule.AvailableWorkMinutes(
            DriverSchedule.DefaultShiftStart,
            DriverSchedule.DefaultShiftEnd,
            DriverSchedule.DefaultLunchMinutes,
            DriverSchedule.DefaultBreakMinutes);
        var slotCount = fixedTemplate is not null
            ? 1
            : drivers.Count > 0 ? Math.Min(vehicles.Count, drivers.Count) : vehicles.Count;
        var maxRouteMinutes = drivers.Count > 0
            ? drivers.Max(d => d.EffectiveRouteCapMinutes)
            : defaultShiftMinutes;

        var depotGeocode = await geocoding.GeocodeAsync(planRun.DepotAddress, ct, tenantId: planRun.TenantId)
            ?? throw new InvalidOperationException("Unable to geocode depot address.");
        var depotPoint = depotGeocode.ToPoint();

        var customersByOrder = await ResolveCustomersByOrderIdAsync(orders, ct);
        var plannable = BuildPlannableOrders(orders, customersByOrder);
        var matrixPoints = CollectMatrixPoints(depotPoint, plannable);
        var planningMatrix = await travelMatrixBuilder.BuildAsync(matrixPoints, ct);

        var useGeographicClustering = fixedTemplate is null
            && policy.Fleet.UseGeographicClustering
            && BalancedRoutePartitioner.ShouldApply(policy.Fleet.BalanceObjective, slotCount, plannable.Count);

        IReadOnlyList<IReadOnlyList<OrderForPlanning>> partitions = useGeographicClustering
            ? GeographicRouteClusterer.PartitionForRoutes(
                plannable,
                slotCount,
                depotPoint,
                planRun.DepotAddress,
                policy.Fleet.BalanceObjective,
                maxStopsPerRoute,
                maxRouteMinutes,
                planningMatrix,
                serviceMinutes)
            : PickupDeliveryRouteOptimizer.PartitionOrdersAcrossVehicles(
                depotPoint,
                planRun.DepotAddress,
                plannable,
                slotCount,
                maxStopsPerRoute,
                planningMatrix,
                maxRouteMinutes,
                serviceMinutes);

        if (!useGeographicClustering &&
            BalancedRoutePartitioner.ShouldApply(policy.Fleet.BalanceObjective, slotCount, plannable.Count))
        {
            partitions = BalancedRoutePartitioner.EnsureBalancedPartitions(
                depotPoint,
                planRun.DepotAddress,
                partitions,
                slotCount,
                policy.Fleet.BalanceObjective,
                maxStopsPerRoute,
                maxRouteMinutes,
                planningMatrix,
                serviceMinutes,
                policy.Fleet.UseGeographicClustering);
        }

        var partitionLists = partitions.Select(p => p.ToList()).ToList();
        if (policy.Fleet.EnforceDriverCaps && drivers.Count > 0 && partitionLists.Count > 0)
        {
            var capsPerRoute = ComputeDriverCapsPerPartition(
                partitionLists,
                drivers,
                policy.Fleet.BalanceObjective,
                depotPoint,
                planRun.DepotAddress,
                planningMatrix,
                serviceMinutes,
                allowMultipleRoutesPerDriver);
            var rebalanced = RouteRebalancer.Rebalance(
                partitionLists,
                capsPerRoute,
                maxStopsPerRoute,
                depotPoint,
                planRun.DepotAddress,
                planningMatrix,
                serviceMinutes);
            partitionLists = rebalanced.Partitions.Select(p => p.ToList()).ToList();
        }

        var proposals = BuildProposals(
            partitionLists,
            vehicles,
            drivers,
            depotPoint,
            planRun.DepotAddress,
            planningMatrix,
            policy.Fleet.BalanceObjective,
            allowMultipleRoutesPerDriver);
        planRun.ProposalCount = proposals.Count;
        planRun.OrdersPlanned = partitionLists.Sum(p => p.Count);
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

    public async Task<PlanReadinessDto> GetPlanReadinessAsync(
        Guid tenantId,
        Guid? depotId = null,
        Guid? fixedRouteTemplateId = null,
        DateOnly? scheduledDate = null,
        CancellationToken ct = default)
    {
        EnsureRoutePlanningAccess(tenantId);
        var policy = await planningPolicyProvider.GetPolicyForTenantAsync(tenantId, ct);

        FixedRouteTemplate? fixedTemplate = null;
        if (fixedRouteTemplateId.HasValue)
        {
            fixedTemplate = await db.FixedRouteTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.Id == fixedRouteTemplateId.Value
                    && t.TenantId == tenantId
                    && t.IsActive, ct)
                ?? throw new ArgumentException("Fixed route template not found.");
        }

        var planDate = scheduledDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (fixedTemplate is not null
            && !FixedRouteScheduleHelper.IncludesDayOfWeek(fixedTemplate.RouteDays, planDate.DayOfWeek))
            planDate = FixedRouteScheduleHelper.ComputeNextRouteDate(fixedTemplate.RouteDays, planDate);

        var heldOrderCount = await db.DeliveryOrders.AsNoTracking()
            .CountAsync(o =>
                o.TenantId == tenantId
                && o.Status == DeliveryOrderStatus.Created
                && o.FixedRouteTemplateId != null, ct);

        var orders = await LoadOrdersForPlanningAsync(
            tenantId,
            null,
            fixedRouteTemplateId,
            planDate,
            ct);
        var customersByOrder = await ResolveCustomersByOrderIdAsync(orders, ct);
        var plannable = BuildPlannableOrders(orders, customersByOrder);
        var windowedOrderCount = plannable.Count(o => o.DeliveryWindow is { HasConstraints: true });

        var allVehicles = await db.Vehicles.AsNoTracking()
            .Where(v => v.TenantId == tenantId)
            .ToListAsync(ct);
        var effectiveDepotId = depotId ?? fixedTemplate?.DepotId;
        var scopedVehicles = effectiveDepotId.HasValue
            ? allVehicles.Where(v => v.HomeDepotId == effectiveDepotId.Value).ToList()
            : allVehicles;
        var activeVehicleCount = scopedVehicles.Count(v => v.Status == VehicleStatus.Active);
        var unassignedCount = allVehicles.Count(v => !v.HomeDepotId.HasValue);

        var allDrivers = await db.Drivers.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(ct);
        var activeDriverCount = allDrivers.Count(d => d.IsActive);
        var workingDriverCount = 0;
        var driversOffCount = 0;

        var notes = new List<string>();
        if (fixedTemplate is not null)
        {
            notes.Add($"Fixed route {fixedTemplate.Name} runs on {FixedRouteScheduleHelper.FormatDaysOfWeek(fixedTemplate.RouteDays)}.");
            notes.Add("Held orders always wait for their fixed route day and are excluded from daily planning.");
            if (orders.Count == 0)
                notes.Add($"No held orders are due for {fixedTemplate.Name} on {planDate:yyyy-MM-dd}.");
        }
        else if (orders.Count == 0)
            notes.Add("No daily-ready orders in Ready status. Held fixed-route orders are excluded.");
        else if (plannable.Count < orders.Count)
            notes.Add($"{orders.Count - plannable.Count} ready order(s) could not be geocoded. Check pickup and delivery addresses.");

        if (heldOrderCount > 0 && fixedTemplate is null)
            notes.Add($"{heldOrderCount} order(s) are held for fixed routes and excluded from daily planning.");

        if (allVehicles.Count == 0)
            notes.Add("No vehicles are registered to this delivery fleet. Add vehicles under Fleet → Vehicles.");
        else if (effectiveDepotId.HasValue && scopedVehicles.Count == 0)
            notes.Add("No vehicles are assigned to this depot. Assign a home depot on each vehicle under Fleet → Vehicles.");
        else if (activeVehicleCount == 0)
            notes.Add(effectiveDepotId.HasValue
                ? "Vehicles are assigned to this depot but none are Active. Mark a vehicle Active under Fleet → Vehicles."
                : "Vehicles exist but none are Active. Mark a vehicle Active under Fleet → Vehicles.");
        else
            notes.Add(effectiveDepotId.HasValue
                ? $"Planning uses {activeVehicleCount} active vehicle(s) assigned to this depot."
                : $"Planning uses all {activeVehicleCount} active vehicle(s) in this fleet.");

        if (unassignedCount > 0 && !effectiveDepotId.HasValue)
            notes.Add($"{unassignedCount} vehicle(s) have no home depot assigned — assign depots under Fleet → Vehicles for accurate multi-depot planning.");

        if (windowedOrderCount > 0)
            notes.Add($"{windowedOrderCount} ready order(s) have customer delivery time windows; sequencing favors open hours and avoids no-delivery blocks where possible.");

        var tenantDelivery = await deliverySettings.GetSettingsAsync(tenantId, ct);
        if (tenantDelivery.AllowMultipleRoutesPerDriverPerDay)
            notes.Add("Multiple routes per driver per day is enabled for this tenant.");

        if (allDrivers.Count == 0)
            notes.Add("No drivers configured — planning uses a default 7:00–17:00 shift (30 min lunch, 15 min breaks). Add drivers under Delivery → Drivers.");
        else if (activeDriverCount == 0)
            notes.Add("Drivers exist but none are Active. Mark drivers Active under Delivery → Drivers.");
        else
        {
            var activeIds = allDrivers.Where(d => d.IsActive).Select(d => d.Id).ToList();
            var resolved = await driverScheduleResolver.ResolveAsync(tenantId, planDate, activeIds, ct);
            var workingDrivers = allDrivers
                .Where(d => d.IsActive && resolved.TryGetValue(d.Id, out var day) && day.IsWorking)
                .ToList();
            var offCount = activeDriverCount - workingDrivers.Count;

            if (workingDrivers.Count == 0)
                notes.Add($"No drivers are scheduled to work on {planDate:dddd, MMM d}. Edit weekly schedules under Delivery → Drivers.");
            else
            {
                workingDriverCount = workingDrivers.Count;
                driversOffCount = offCount;

                var minAvailable = workingDrivers
                    .Select(d => resolved[d.Id].AvailableWorkMinutes)
                    .DefaultIfEmpty(0)
                    .Min();
                notes.Add($"Planning uses {workingDrivers.Count} driver(s) scheduled for {planDate:dddd} (shortest shift {DriverSchedule.FormatWorkMinutes(minAvailable)} after lunch and breaks).");
                if (offCount > 0)
                    notes.Add($"{offCount} active driver(s) are off on {planDate:dddd} per their weekly schedule.");
                if (workingDrivers.Count < activeVehicleCount)
                    notes.Add($"Only {Math.Min(activeVehicleCount, workingDrivers.Count)} route(s) can run in parallel — fewer working drivers than active vehicles.");

                foreach (var driver in workingDrivers)
                {
                    var day = resolved[driver.Id];
                    var cap = PlanningPolicyResolver.ResolveEffectiveRouteCapMinutes(day);
                    if (cap < day.AvailableWorkMinutes)
                        notes.Add($"{driver.DisplayName}: route capped at {DriverSchedule.FormatWorkMinutes(cap)} for {planDate:dddd}.");
                }

                if (fixedTemplate is null)
                    await AppendDailyDriverConflictNotesAsync(
                        notes, tenantId, planDate, workingDrivers, tenantDelivery.AllowMultipleRoutesPerDriverPerDay, ct);
            }
        }

        if (!string.Equals(policy.Fleet.BalanceObjective, "none", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add($"Truck balance: {policy.Fleet.BalanceObjective} (max {policy.Fleet.MaxImbalancePercent}% imbalance).");
            if (activeVehicleCount > 1 && activeDriverCount > 1 && plannable.Count > 1)
            {
                notes.Add($"Planning will spread orders across up to {Math.Min(activeVehicleCount, activeDriverCount)} route(s) when possible.");
                if (policy.Fleet.UseGeographicClustering)
                    notes.Add("Multi-truck plans cluster stops by geography before sequencing to reduce cross-region driving.");
                else
                    notes.Add("Geographic clustering is off — routes are balanced by minutes/stops only.");
            }
        }
        notes.Add($"Max {policy.Fleet.MaxStopsPerRoute} stops per route (tenant planning rules).");
        if (policy.Fleet.EnforceDriverCaps)
            notes.Add("Driver route caps are enforced — orders that cannot fit a driver cap stay unassigned.");

        var canPlan = orders.Count > 0 && plannable.Count > 0 && activeVehicleCount > 0;
        return new PlanReadinessDto(
            orders.Count,
            plannable.Count,
            scopedVehicles.Count,
            activeVehicleCount,
            allDrivers.Count,
            activeDriverCount,
            canPlan,
            notes,
            windowedOrderCount,
            heldOrderCount,
            fixedRouteTemplateId,
            fixedTemplate is null ? 0 : orders.Count,
            workingDriverCount,
            driversOffCount,
            planDate);
    }

    public async Task<RoutePlanRunDto?> GetPlanRunAsync(Guid planRunId, CancellationToken ct = default)
    {
        var planRun = await ScopedPlanRuns().AsNoTracking().FirstOrDefaultAsync(p => p.Id == planRunId, ct);
        return planRun is null ? null : await MapPlanRunAsync(planRun, ct);
    }

    public async Task<PlanRunExplanationDto> ExplainPlanRunAsync(Guid planRunId, CancellationToken ct = default)
    {
        var planRun = await ScopedPlanRuns().AsNoTracking().FirstOrDefaultAsync(p => p.Id == planRunId, ct)
            ?? throw new InvalidOperationException("Plan run not found.");
        EnsureRoutePlanningAccess(planRun.TenantId);

        var dto = await MapPlanRunAsync(planRun, ct);
        var planJson = JsonSerializer.Serialize(dto, JsonOptions);
        var rules = await db.TenantPlanningRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == planRun.TenantId, ct);

        var result = await planRunExplainer.ExplainAsync(
            planRun.TenantId,
            planJson,
            rules?.CompiledPolicyJson,
            ct);

        return new PlanRunExplanationDto(planRunId, result.Explanation, result.UsedAi);
    }

    public async Task<IReadOnlyList<RoutePlanRunDto>> GetPlanRunsAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureRoutePlanningAccess(tenantId);
        var runs = await ScopedPlanRuns().AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (runs.Count == 0)
            return [];

        var userIds = runs.Select(r => r.RequestedByUserId).Distinct().ToList();
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? "Unknown", ct);

        return runs
            .Select(run => MapPlanRunSummary(run, userNames.GetValueOrDefault(run.RequestedByUserId, "Unknown")))
            .ToList();
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
        var depotGeocode = await geocoding.GeocodeAsync(planRun.DepotAddress, ct, tenantId: planRun.TenantId);
        if (depotGeocode.HasValue)
            proposals = FilterDepotPickupStops(proposals, planRun.DepotAddress, depotGeocode.Value.ToPoint());
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
        var fixedTemplateName = planRun.FixedRouteTemplateId.HasValue
            ? await db.FixedRouteTemplates.AsNoTracking()
                .Where(t => t.Id == planRun.FixedRouteTemplateId.Value)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct)
            : null;

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

            var routeName = !string.IsNullOrWhiteSpace(fixedTemplateName)
                ? $"{fixedTemplateName} — {planRun.ScheduledDate:yyyy-MM-dd}"
                : $"Plan {planRun.ScheduledDate:yyyy-MM-dd} #{routeIndex++}";
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
            route.FixedRouteTemplateId = planRun.FixedRouteTemplateId;
            if (proposal.VehicleId.HasValue)
            {
                route.VehicleId = proposal.VehicleId;
                route.AutomationMode = AutomationMode.Conventional;
            }
            if (proposal.DriverId.HasValue)
                route.DriverId = proposal.DriverId;
            await db.SaveChangesAsync(ct);
            if (proposal.VehicleId.HasValue)
                await deliveryService.SyncRouteOrderAssignmentsAsync(created.Id, ct);
            createdRoutes.Add(new AcceptedRouteSummaryDto(created.Id, routeName));
        }

        if (ordersById.Count > 0)
        {
            foreach (var order in ordersById.Values.Where(o => o.Status == DeliveryOrderStatus.Created))
            {
                order.Status = DeliveryOrderStatus.Assigned;
                order.FixedRouteTemplateId = null;
                order.HeldUntil = null;
            }
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
            ct,
            route.TenantId);
        if (!geocoded.TryGetValue(route.DepotAddress, out var depotGeocode))
            throw new InvalidOperationException("Unable to geocode depot address.");
        var depotPoint = depotGeocode.ToPoint();

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
        var orderWindows = new Dictionary<Guid, CustomerDeliveryWindow>();
        if (linkedOrderIds.Count > 0)
        {
            var orders = await db.DeliveryOrders.AsNoTracking()
                .Where(o => linkedOrderIds.Contains(o.Id))
                .ToListAsync(ct);
            var customersByOrder = await ResolveCustomersByOrderIdAsync(orders, ct);
            foreach (var order in orders)
            {
                GeoPoint? pickup = order.PickupLatitude is decimal lat && order.PickupLongitude is decimal lng
                    ? new GeoPoint(lat, lng)
                    : null;
                if (DepotPickupMatcher.IsPickupAtDepot(order.PickupAddress, pickup, depotPoint, route.DepotAddress))
                    pickedUpAtDepot.Add(order.Id);

                if (customersByOrder.TryGetValue(order.Id, out var customer))
                {
                    var window = ToDeliveryWindow(customer);
                    if (window.HasConstraints)
                        orderWindows[order.Id] = window;
                }
            }
        }

        var routeStart = DriverSchedule.DefaultShiftStart;
        if (route.DriverId.HasValue)
        {
            var driver = await db.Drivers.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == route.DriverId.Value, ct);
            if (driver is not null)
                routeStart = DriverSchedule.NormalizeShiftStart(driver.ShiftStartTime);
        }

        var stopById = operationalStops.ToDictionary(s => s.Id);
        var requestedStops = request.StopIds.Select(id => stopById[id]).ToList();
        var planningStops = new List<PlanningStop>();
        foreach (var stop in requestedStops)
        {
            if (!geocoded.TryGetValue(stop.Address, out var stopGeocode))
                throw new InvalidOperationException($"Unable to geocode stop address: {stop.Address}");

            CustomerDeliveryWindow? window = null;
            if (stop.StopType == DeliveryStopType.Dropoff
                && stop.DeliveryOrderId.HasValue
                && orderWindows.TryGetValue(stop.DeliveryOrderId.Value, out var orderWindow))
                window = orderWindow;

            planningStops.Add(new PlanningStop(
                stop.DeliveryOrderId,
                stop.StopType == DeliveryStopType.Pickup ? "Pickup" : "Dropoff",
                stop.Address,
                stopGeocode.ToPoint(),
                stop.RecipientName,
                stop.ParcelDescription,
                stop.Id,
                window));
        }

        var serviceMinutes = planningOptions.Value.DefaultServiceMinutes;
        var beforeMinutes = PickupDeliveryRouteOptimizer.EstimateRouteMinutes(
            depotPoint, planningStops, travelTimeMatrix, serviceMinutes);

        var optimizedNodes = PickupDeliveryRouteOptimizer.OptimizeStopSequence(
            depotPoint,
            planningStops,
            travelTimeMatrix,
            pickedUpAtDepot,
            routeStart,
            serviceMinutes);
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

    private async Task<List<DeliveryOrder>> LoadOrdersForPlanningAsync(
        Guid tenantId,
        IReadOnlyList<Guid>? orderIds,
        Guid? fixedRouteTemplateId,
        DateOnly scheduledDate,
        CancellationToken ct)
    {
        var query = db.DeliveryOrders
            .Where(o => o.TenantId == tenantId && o.Status == DeliveryOrderStatus.Created);

        if (fixedRouteTemplateId.HasValue)
        {
            query = query.Where(o =>
                o.FixedRouteTemplateId == fixedRouteTemplateId
                && o.HeldUntil.HasValue
                && o.HeldUntil.Value <= scheduledDate);
        }
        else
        {
            query = query.Where(o => o.FixedRouteTemplateId == null);
        }

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

    private sealed record DriverPlanningSlot(
        Guid Id,
        string Label,
        int AvailableMinutes,
        int EffectiveRouteCapMinutes,
        string ShiftWindow,
        TimeOnly ShiftStart,
        TimeOnly ShiftEnd,
        int LunchMinutes,
        TimeOnly? OffBlockStart,
        TimeOnly? OffBlockEnd);

    private async Task<List<Driver>> LoadDriverEntitiesForPlanningAsync(
        Guid tenantId,
        IReadOnlyList<Guid>? driverIds,
        CancellationToken ct)
    {
        var query = db.Drivers.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.IsActive);
        if (driverIds is { Count: > 0 })
            query = query.Where(d => driverIds.Contains(d.Id));

        return await query.OrderBy(d => d.DisplayName).ToListAsync(ct);
    }

    private static List<DriverPlanningSlot> BuildDriverPlanningSlots(
        IReadOnlyList<Driver> drivers,
        IReadOnlyDictionary<Guid, ResolvedDriverDay> resolvedByDriver) =>
        drivers
            .Where(d => resolvedByDriver.TryGetValue(d.Id, out var day) && day.IsWorking)
            .Select(d =>
            {
                var day = resolvedByDriver[d.Id];
                var available = day.AvailableWorkMinutes;
                var cap = PlanningPolicyResolver.ResolveEffectiveRouteCapMinutes(day);
                return new DriverPlanningSlot(
                    d.Id,
                    d.DisplayName,
                    available,
                    cap,
                    DriverSchedule.FormatShiftWindow(day.ShiftStart, day.ShiftEnd),
                    DriverSchedule.NormalizeShiftStart(day.ShiftStart),
                    DriverSchedule.NormalizeShiftEnd(day.ShiftEnd),
                    day.LunchMinutes,
                    day.OffBlockStart,
                    day.OffBlockEnd);
            })
            .ToList();

    private async Task<Dictionary<Guid, Customer>> LoadCustomersForOrdersAsync(
        IReadOnlyList<DeliveryOrder> orders,
        CancellationToken ct)
    {
        var customerIds = orders
            .Where(o => o.CustomerId.HasValue)
            .Select(o => o.CustomerId!.Value)
            .Distinct()
            .ToList();

        if (customerIds.Count == 0)
            return [];

        return await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);
    }

    private async Task<IReadOnlyDictionary<Guid, Customer>> ResolveCustomersByOrderIdAsync(
        IReadOnlyList<DeliveryOrder> orders,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, Customer>();
        if (orders.Count == 0)
            return result;

        var byCustomerId = await LoadCustomersForOrdersAsync(orders, ct);
        var unlinked = orders.Where(o => !o.CustomerId.HasValue).ToList();
        var byTenantAndName = new Dictionary<(Guid TenantId, string Name), List<Customer>>();

        if (unlinked.Count > 0)
        {
            var tenantIds = unlinked.Select(o => o.TenantId).Distinct().ToList();
            var tenantCustomers = await db.Customers.AsNoTracking()
                .Where(c => tenantIds.Contains(c.TenantId) && c.IsActive)
                .ToListAsync(ct);

            foreach (var customer in tenantCustomers)
            {
                var key = (customer.TenantId, NormalizeRecipientName(customer.Name));
                if (!byTenantAndName.TryGetValue(key, out var list))
                {
                    list = [];
                    byTenantAndName[key] = list;
                }

                list.Add(customer);
            }
        }

        foreach (var order in orders)
        {
            Customer? customer = null;
            if (order.CustomerId.HasValue)
                byCustomerId.TryGetValue(order.CustomerId.Value, out customer);
            else if (byTenantAndName.TryGetValue((order.TenantId, NormalizeRecipientName(order.RecipientName)), out var matches))
                customer = PickBestCustomerMatch(order, matches);

            if (customer is not null)
                result[order.Id] = customer;
        }

        return result;
    }

    private static Customer? PickBestCustomerMatch(DeliveryOrder order, IReadOnlyList<Customer> matches)
    {
        if (matches.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(order.RecipientPhone))
        {
            var phoneMatch = matches.FirstOrDefault(c =>
                string.Equals(c.Phone, order.RecipientPhone, StringComparison.OrdinalIgnoreCase));
            if (phoneMatch is not null)
                return phoneMatch;
        }

        return matches[0];
    }

    private static string NormalizeRecipientName(string name) =>
        name.Trim().ToUpperInvariant();

    private List<OrderForPlanning> BuildPlannableOrders(
        IReadOnlyList<DeliveryOrder> orders,
        IReadOnlyDictionary<Guid, Customer> customersByOrderId)
    {
        var plannable = new List<OrderForPlanning>();
        foreach (var order in orders)
        {
            var pickup = orderGeocoding.PickupPoint(order);
            var delivery = orderGeocoding.DeliveryPoint(order);
            if (pickup is null || delivery is null)
                continue;

            CustomerDeliveryWindow? window = null;
            if (customersByOrderId.TryGetValue(order.Id, out var customer))
            {
                window = ToDeliveryWindow(customer);
                if (window is { HasConstraints: false })
                    window = null;
            }

            plannable.Add(new OrderForPlanning(
                order.Id,
                order.PickupAddress,
                order.DeliveryAddress,
                pickup.Value,
                delivery.Value,
                order.RecipientName,
                order.ParcelDescription,
                window));
        }

        return plannable;
    }

    private static CustomerDeliveryWindow ToDeliveryWindow(Customer customer) =>
        new(
            customer.DeliveryWindowStart,
            customer.DeliveryWindowEnd,
            customer.NoDeliveryStart,
            customer.NoDeliveryEnd);

    private static List<GeoPoint> CollectMatrixPoints(GeoPoint depot, IReadOnlyList<OrderForPlanning> orders)
    {
        var seen = new HashSet<(long Longitude, long Latitude)>();
        var points = new List<GeoPoint>();

        void Add(GeoPoint point)
        {
            var key = (RoundCoord(point.Longitude), RoundCoord(point.Latitude));
            if (seen.Add(key))
                points.Add(point);
        }

        Add(depot);
        foreach (var order in orders)
        {
            Add(order.Pickup);
            Add(order.Delivery);
        }

        return points;
    }

    private static long RoundCoord(decimal value) =>
        (long)Math.Round((double)value * 100_000, MidpointRounding.AwayFromZero);

    private static IReadOnlyList<int> ComputeDriverCapsPerPartition(
        IReadOnlyList<IReadOnlyList<OrderForPlanning>> partitions,
        IReadOnlyList<DriverPlanningSlot> drivers,
        string balanceObjective,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix,
        int serviceMinutes,
        bool allowMultipleRoutesPerDriverPerDay)
    {
        var partitionWeights = partitions
            .Select(p => string.Equals(balanceObjective, "stopCount", StringComparison.OrdinalIgnoreCase)
                ? p.Count
                : EstimatePartitionMinutes(p, depot, depotAddress, matrix, serviceMinutes))
            .ToList();

        var driverSlots = RouteAssignmentBalancer.AssignPartitionsToDrivers(
            partitionWeights,
            drivers.Select(d => d.EffectiveRouteCapMinutes).ToList(),
            drivers.Select(d => d.AvailableMinutes).ToList(),
            balanceObjective,
            allowMultipleRoutesPerDriverPerDay);

        var caps = new List<int>(partitions.Count);
        for (var i = 0; i < partitions.Count; i++)
        {
            var driverIndex = driverSlots.Count > i
                ? driverSlots[i]
                : Math.Min(i, drivers.Count - 1);
            caps.Add(drivers[Math.Min(driverIndex, drivers.Count - 1)].EffectiveRouteCapMinutes);
        }

        return caps;
    }

    private List<PlannedRouteProposalDto> BuildProposals(
        IReadOnlyList<IReadOnlyList<OrderForPlanning>> partitions,
        IReadOnlyList<(Guid Id, string Label)> vehicles,
        IReadOnlyList<DriverPlanningSlot> drivers,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix,
        string balanceObjective,
        bool allowMultipleRoutesPerDriverPerDay)
    {
        var serviceMinutes = planningOptions.Value.DefaultServiceMinutes;
        var proposals = new List<PlannedRouteProposalDto>();
        if (partitions.Count == 0)
            return proposals;

        var partitionWeights = partitions
            .Select(p => string.Equals(balanceObjective, "stopCount", StringComparison.OrdinalIgnoreCase)
                ? p.Count
                : EstimatePartitionMinutes(p, depot, depotAddress, matrix, serviceMinutes))
            .ToList();

        var vehicleSlots = RouteAssignmentBalancer.AssignPartitionsToSlots(
            partitionWeights,
            vehicles.Count,
            balanceObjective);

        IReadOnlyList<int> driverSlots = drivers.Count > 0
            ? RouteAssignmentBalancer.AssignPartitionsToDrivers(
                partitionWeights,
                drivers.Select(d => d.EffectiveRouteCapMinutes).ToList(),
                drivers.Select(d => d.AvailableMinutes).ToList(),
                balanceObjective,
                allowMultipleRoutesPerDriverPerDay)
            : [];

        for (var i = 0; i < partitions.Count; i++)
        {
            var vehicleIndex = vehicleSlots.Count > i ? vehicleSlots[i] : Math.Min(i, vehicles.Count - 1);
            var vehicle = vehicles[Math.Min(vehicleIndex, vehicles.Count - 1)];

            DriverPlanningSlot? driver = null;
            if (drivers.Count > 0)
            {
                var driverIndex = driverSlots.Count > i
                    ? driverSlots[i]
                    : Math.Min(i, drivers.Count - 1);
                driver = drivers[Math.Min(driverIndex, drivers.Count - 1)];
            }

            var routeOrders = partitions[i];
            var routeStart = DriverSchedule.NormalizeShiftStart(driver?.ShiftStart ?? DriverSchedule.DefaultShiftStart);
            var rawStops = PickupDeliveryRouteOptimizer.BuildPlanningStops(routeOrders, depot, depotAddress);
            var pickedUpAtDepot = PickupDeliveryRouteOptimizer.PickedUpAtDepotOrderIds(routeOrders, depot, depotAddress);

            var optimized = PickupDeliveryRouteOptimizer.OptimizeStopSequence(
                depot,
                rawStops,
                matrix,
                pickedUpAtDepot,
                routeStart,
                serviceMinutes);
            var schedule = RouteScheduleSimulator.Simulate(
                depot,
                optimized,
                matrix,
                serviceMinutes,
                routeStart,
                driver?.ShiftEnd,
                driver?.LunchMinutes ?? 0,
                driver?.OffBlockStart,
                driver?.OffBlockEnd);
            var plannedStops = MapPlannedStops(optimized, schedule);
            var warnings = BuildWindowWarnings(optimized, schedule);
            var violationCount = schedule.Sum(s => s.Violations.Count);

            var estimatedMinutes = PickupDeliveryRouteOptimizer.EstimateRouteMinutes(
                depot, optimized, matrix, serviceMinutes);

            if (driver is not null && estimatedMinutes > driver.EffectiveRouteCapMinutes)
            {
                warnings = warnings.Concat([
                    $"Route exceeds {driver.Label}'s cap ({driver.EffectiveRouteCapMinutes} min) — est. {estimatedMinutes} min."
                ]).ToList();
            }

            proposals.Add(new PlannedRouteProposalDto(
                vehicle.Id,
                vehicle.Label,
                estimatedMinutes,
                plannedStops,
                driver?.Id,
                driver?.Label,
                driver?.EffectiveRouteCapMinutes,
                driver?.ShiftWindow,
                RouteScheduleSimulator.FormatTime(routeStart),
                warnings,
                violationCount));
        }

        return proposals;
    }

    private static int EstimatePartitionMinutes(
        IReadOnlyList<OrderForPlanning> orders,
        GeoPoint depot,
        string depotAddress,
        ITravelTimeMatrix matrix,
        int serviceMinutes)
    {
        if (orders.Count == 0)
            return 0;

        var stops = PickupDeliveryRouteOptimizer.BuildPlanningStops(orders, depot, depotAddress);
        return PickupDeliveryRouteOptimizer.EstimateRouteMinutes(depot, stops, matrix, serviceMinutes);
    }

    private static List<PlannedStopDto> MapPlannedStops(
        IReadOnlyList<PlanningStop> stops,
        IReadOnlyList<SimulatedStopSchedule> schedule)
    {
        var planned = new List<PlannedStopDto>(stops.Count);
        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            var sim = schedule[i];
            var window = stop.DeliveryWindow;
            planned.Add(new PlannedStopDto(
                stop.OrderId,
                stop.StopType,
                i + 1,
                stop.Address,
                stop.RecipientName,
                stop.Location.Latitude,
                stop.Location.Longitude,
                stop.ParcelDescription,
                CustomerDeliveryWindow.Format(window?.OpenStart),
                CustomerDeliveryWindow.Format(window?.OpenEnd),
                CustomerDeliveryWindow.Format(window?.NoDeliveryStart),
                CustomerDeliveryWindow.Format(window?.NoDeliveryEnd),
                RouteScheduleSimulator.FormatTime(sim.Arrival),
                sim.Violations.Select(v => v.Message).ToList()));
        }

        return planned;
    }

    private static IReadOnlyList<string> BuildWindowWarnings(
        IReadOnlyList<PlanningStop> stops,
        IReadOnlyList<SimulatedStopSchedule> schedule)
    {
        var warnings = new List<string>();
        for (var i = 0; i < stops.Count; i++)
        {
            if (stops[i].StopType != "Dropoff" || schedule[i].Violations.Count == 0)
                continue;

            var label = stops[i].RecipientName ?? stops[i].Address;
            foreach (var violation in schedule[i].Violations)
                warnings.Add($"{label}: {violation.Message}");
        }

        return warnings;
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

    private static RoutePlanRunDto MapPlanRunSummary(RoutePlanRun planRun, string requestedByName) =>
        new(
            planRun.Id,
            planRun.TenantId,
            planRun.RequestedByUserId,
            requestedByName,
            planRun.Status,
            planRun.ScheduledDate,
            planRun.DepotAddress,
            null,
            null,
            planRun.OrdersRequested,
            planRun.OrdersPlanned,
            planRun.OrdersUnassigned,
            planRun.ProposalCount,
            planRun.ComputeDurationMs,
            [],
            planRun.CreatedAt,
            planRun.CompletedAt,
            planRun.ResolvedAt);

    private async Task<RoutePlanRunDto> MapPlanRunAsync(RoutePlanRun planRun, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == planRun.RequestedByUserId, ct);
        var (depotLat, depotLng) = await ResolveDepotCoordinatesAsync(planRun, ct);

        var proposals = await RefreshProposalsFromOrdersAsync(DeserializeProposals(planRun.ProposalJson), ct);
        if (depotLat.HasValue && depotLng.HasValue)
        {
            var depot = new GeoPoint(depotLat.Value, depotLng.Value);
            proposals = FilterDepotPickupStops(proposals, planRun.DepotAddress, depot);
            proposals = EnrichProposalsWithSchedule(proposals, depot);
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

    private async Task<(decimal? Latitude, decimal? Longitude)> ResolveDepotCoordinatesAsync(
        RoutePlanRun planRun,
        CancellationToken ct)
    {
        if (planRun.DepotId.HasValue)
        {
            var depot = await db.Depots.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == planRun.DepotId.Value, ct);
            if (depot?.Latitude is decimal lat && depot.Longitude is decimal lng)
                return (lat, lng);
        }

        if (string.IsNullOrWhiteSpace(planRun.DepotAddress))
            return (null, null);

        var geocoded = await geocoding.GeocodeAsync(planRun.DepotAddress, ct, tenantId: planRun.TenantId);
        return geocoded.HasValue
            ? (geocoded.Value.Latitude, geocoded.Value.Longitude)
            : (null, null);
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
            .ToListAsync(ct);
        var customersByOrder = await ResolveCustomersByOrderIdAsync(orders, ct);
        var snapshots = orders.ToDictionary(o => o.Id, ToOrderSnapshot);
        var windows = customersByOrder
            .ToDictionary(
                pair => pair.Key,
                pair =>
                {
                    var window = ToDeliveryWindow(pair.Value);
                    return window.HasConstraints ? window : null;
                });

        return PlanProposalSync.RefreshFromOrders(proposals, snapshots, windows);
    }

    private IReadOnlyList<PlannedRouteProposalDto> EnrichProposalsWithSchedule(
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
                    .OrderBy(s => s.Sequence)
                    .Select(s => new PlanningStop(
                        s.OrderId,
                        s.StopType,
                        s.Address,
                        new GeoPoint(s.Latitude!.Value, s.Longitude!.Value),
                        s.RecipientName,
                        s.ParcelDescription,
                        DeliveryWindow: DeliveryWindowFromDto(s)))
                    .ToList();

                if (planningStops.Count == 0)
                    return proposal;

                var routeStart = ResolveRouteStartTime(proposal.EstimatedRouteStart, proposal.ShiftWindow);
                var (shiftEnd, lunchMinutes) = ResolveLunchFromProposal(proposal);
                var schedule = RouteScheduleSimulator.Simulate(
                    depot,
                    planningStops,
                    travelTimeMatrix,
                    serviceMinutes,
                    routeStart,
                    shiftEnd,
                    lunchMinutes,
                    offBlockStart: null,
                    offBlockEnd: null);
                var warnings = BuildWindowWarnings(planningStops, schedule);
                var violationCount = schedule.Sum(s => s.Violations.Count);

                return proposal with
                {
                    EstimatedMinutes = PickupDeliveryRouteOptimizer.EstimateRouteMinutes(
                        depot,
                        planningStops,
                        travelTimeMatrix,
                        serviceMinutes),
                    EstimatedRouteStart = RouteScheduleSimulator.FormatTime(routeStart),
                    Stops = MapPlannedStopsFromDto(proposal.Stops, schedule),
                    Warnings = warnings,
                    WindowViolationCount = violationCount,
                };
            })
            .ToList();
    }

    private static List<PlannedStopDto> MapPlannedStopsFromDto(
        IReadOnlyList<PlannedStopDto> stops,
        IReadOnlyList<SimulatedStopSchedule> schedule)
    {
        var operational = stops
            .Where(s => s.StopType is "Pickup" or "Dropoff")
            .OrderBy(s => s.Sequence)
            .ToList();

        if (operational.Count != schedule.Count)
            return stops.ToList();

        var scheduleBySequence = operational
            .Select((stop, index) => (stop.Sequence, schedule[index]))
            .ToDictionary(x => x.Sequence, x => x.Item2);

        return stops
            .Select(stop =>
            {
                if (!scheduleBySequence.TryGetValue(stop.Sequence, out var sim))
                    return stop;

                return stop with
                {
                    EstimatedArrival = RouteScheduleSimulator.FormatTime(sim.Arrival),
                    WindowWarnings = sim.Violations.Select(v => v.Message).ToList(),
                };
            })
            .ToList();
    }

    private static CustomerDeliveryWindow? DeliveryWindowFromDto(PlannedStopDto stop)
    {
        if (stop.DeliveryOpenStart is null
            && stop.DeliveryOpenEnd is null
            && stop.NoDeliveryStart is null
            && stop.NoDeliveryEnd is null)
            return null;

        return new CustomerDeliveryWindow(
            ParseRouteStartTime(stop.DeliveryOpenStart),
            ParseRouteStartTime(stop.DeliveryOpenEnd),
            ParseRouteStartTime(stop.NoDeliveryStart),
            ParseRouteStartTime(stop.NoDeliveryEnd));
    }

    private static TimeOnly? ParseRouteStartTime(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : TimeOnly.Parse(value);

    private static (TimeOnly? ShiftEnd, int LunchMinutes) ResolveLunchFromProposal(PlannedRouteProposalDto proposal)
    {
        TimeOnly? shiftEnd = null;
        if (!string.IsNullOrWhiteSpace(proposal.ShiftWindow))
        {
            var dash = proposal.ShiftWindow.IndexOf('–');
            if (dash < 0)
                dash = proposal.ShiftWindow.IndexOf('-');
            if (dash > 0 && TimeOnly.TryParse(proposal.ShiftWindow[(dash + 1)..].Trim(), out var end))
                shiftEnd = end;
        }

        return (shiftEnd, DriverSchedule.DefaultLunchMinutes);
    }

    private async Task<HashSet<Guid>> GetDriversUnavailableForDailyPlanningAsync(
        Guid tenantId,
        DateOnly planDate,
        bool allowMultipleRoutesPerDriver,
        CancellationToken ct)
    {
        var unavailable = new HashSet<Guid>();
        if (allowMultipleRoutesPerDriver)
            return unavailable;

        var fixedTemplates = await db.FixedRouteTemplates.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive && t.DefaultDriverId.HasValue)
            .ToListAsync(ct);

        foreach (var template in fixedTemplates.Where(t => t.RouteDays.Contains(planDate.DayOfWeek)))
        {
            if (template.DefaultDriverId.HasValue)
                unavailable.Add(template.DefaultDriverId.Value);
        }

        var assignedDrivers = await db.DeliveryRoutes.AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.ScheduledDate == planDate
                && r.DriverId.HasValue
                && r.Status != DeliveryRouteStatus.Cancelled)
            .Select(r => r.DriverId!.Value)
            .Distinct()
            .ToListAsync(ct);

        foreach (var driverId in assignedDrivers)
            unavailable.Add(driverId);

        return unavailable;
    }

    private async Task AppendDailyDriverConflictNotesAsync(
        List<string> notes,
        Guid tenantId,
        DateOnly planDate,
        IReadOnlyList<Driver> workingDrivers,
        bool allowMultipleRoutesPerDriver,
        CancellationToken ct)
    {
        var fixedTemplates = await db.FixedRouteTemplates.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive && t.DefaultDriverId.HasValue)
            .ToListAsync(ct);

        var routesOnDate = await db.DeliveryRoutes.AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.ScheduledDate == planDate
                && r.DriverId.HasValue
                && r.Status != DeliveryRouteStatus.Cancelled)
            .Select(r => new { r.DriverId, r.Name })
            .ToListAsync(ct);

        var routesByDriver = routesOnDate
            .GroupBy(r => r.DriverId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());

        foreach (var driver in workingDrivers)
        {
            var fixedForDay = fixedTemplates
                .Where(t => t.DefaultDriverId == driver.Id && t.RouteDays.Contains(planDate.DayOfWeek))
                .ToList();

            if (fixedForDay.Count > 0 && !allowMultipleRoutesPerDriver)
            {
                notes.Add(
                    $"{driver.DisplayName} is the default driver for fixed route {fixedForDay[0].Name} on {planDate:dddd} and will be excluded from daily planning.");
            }
            else if (fixedForDay.Count > 0)
            {
                notes.Add(
                    $"{driver.DisplayName} is assigned to fixed route {string.Join(", ", fixedForDay.Select(f => f.Name))} on {planDate:dddd} (multi-route enabled).");
            }

            if (routesByDriver.TryGetValue(driver.Id, out var routeNames))
            {
                notes.Add(
                    $"{driver.DisplayName} already has route(s) on {planDate:yyyy-MM-dd}: {string.Join(", ", routeNames)}.");
            }
        }
    }

    private static TimeOnly ResolveRouteStartTime(string? estimatedRouteStart, string? shiftWindow)
    {
        if (TryParseShiftWindowStart(shiftWindow, out var shiftStart) && shiftStart != default)
            return DriverSchedule.NormalizeShiftStart(shiftStart);

        var parsed = ParseRouteStartTime(estimatedRouteStart);
        if (parsed.HasValue && parsed.Value != default)
            return parsed.Value;

        return DriverSchedule.DefaultShiftStart;
    }

    private static bool TryParseShiftWindowStart(string? shiftWindow, out TimeOnly start)
    {
        start = default;
        if (string.IsNullOrWhiteSpace(shiftWindow))
            return false;

        var dash = shiftWindow.IndexOf('–');
        if (dash < 0)
            dash = shiftWindow.IndexOf('-');
        if (dash <= 0)
            return false;

        return TimeOnly.TryParse(shiftWindow[..dash].Trim(), out start);
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
