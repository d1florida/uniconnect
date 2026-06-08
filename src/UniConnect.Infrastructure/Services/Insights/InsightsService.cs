using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;
using UniConnect.Insights.Enums;
using UniConnect.Insights.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services.Insights;

public class InsightsService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IDriverDirectory drivers,
    ICustomerDirectory customers) : IInsightsService
{
    public async Task<TenantDigestDto> GetTenantDigestAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        EnsureInsightsAccess(tenantId);
        var (fromUtc, toUtc) = ToUtcRange(from, to);

        var deliveryEvents = await FilterEvents(tenantId, fromUtc, toUtc, InsightsDomains.Delivery, null, null, null, null, null)
            .ToListAsync(ct);

        var deliveryKpis = BuildKpis(deliveryEvents);

        PlannerDigestDto? planning = null;
        if (currentUser.HasModule(ProductModule.RoutePlanning))
        {
            var planningEvents = await FilterEvents(tenantId, fromUtc, toUtc, InsightsDomains.RoutePlanning, null, null, null, null, null)
                .ToListAsync(ct);
            var requested = planningEvents.Count(e => e.EventType == RoutePlanningEventTypes.PlanRequested);
            var accepted = planningEvents.Count(e => e.EventType == RoutePlanningEventTypes.PlanAccepted);
            var activePlanners = planningEvents.Where(e => e.UserId.HasValue).Select(e => e.UserId!.Value).Distinct().Count();
            planning = new PlannerDigestDto(
                activePlanners,
                requested,
                accepted,
                requested == 0 ? 0 : Math.Round(accepted / (double)requested, 2));
        }

        return new TenantDigestDto(tenantId, from, to, deliveryKpis, planning);
    }

    public Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureInsightsAccess(tenantId);
        return customers.GetCustomersAsync(tenantId, includeInactive: false, ct);
    }

    public Task<IReadOnlyList<DriverDto>> GetDriversAsync(Guid tenantId, CancellationToken ct = default)
    {
        EnsureInsightsAccess(tenantId);
        return drivers.GetDriversAsync(tenantId, ct);
    }

    public async Task<SubjectSummaryDto?> GetDriverSummaryAsync(Guid tenantId, Guid driverId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        EnsureInsightsAccess(tenantId);
        var driver = await db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == tenantId, ct);
        if (driver is null) return null;
        return await BuildSubjectSummaryAsync("driver", driverId, driver.DisplayName, tenantId, from, to, driverId, null, null, null, ct);
    }

    public async Task<SubjectSummaryDto?> GetVehicleSummaryAsync(Guid tenantId, Guid vehicleId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        EnsureInsightsAccess(tenantId);
        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId && v.TenantId == tenantId, ct);
        if (vehicle is null) return null;
        return await BuildSubjectSummaryAsync("vehicle", vehicleId, vehicle.VehicleNumber, tenantId, from, to, null, vehicleId, null, null, ct);
    }

    public async Task<SubjectSummaryDto?> GetCustomerSummaryAsync(Guid tenantId, Guid customerId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        EnsureInsightsAccess(tenantId);
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId, ct);
        if (customer is null) return null;
        return await BuildSubjectSummaryAsync("customer", customerId, customer.Name, tenantId, from, to, null, null, customerId, null, ct);
    }

    public async Task<PlannerSummaryDto?> GetPlannerSummaryAsync(Guid tenantId, Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        EnsureInsightsAndPlanningAccess(tenantId);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        if (user is null) return null;

        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var events = await FilterEvents(tenantId, fromUtc, toUtc, InsightsDomains.RoutePlanning, null, null, null, userId, null)
            .ToListAsync(ct);

        var requested = events.Count(e => e.EventType == RoutePlanningEventTypes.PlanRequested);
        var accepted = events.Count(e => e.EventType == RoutePlanningEventTypes.PlanAccepted);
        var discarded = events.Count(e => e.EventType == RoutePlanningEventTypes.PlanDiscarded);
        var ordersPlanned = events
            .Where(e => e.EventType == RoutePlanningEventTypes.PlanCompleted)
            .Sum(e => ReadIntMetric(e, "ordersPlanned"));

        return new PlannerSummaryDto(
            userId,
            user.DisplayName,
            user.Email,
            from,
            to,
            requested,
            accepted,
            discarded,
            ordersPlanned,
            requested == 0 ? 0 : Math.Round(accepted / (double)requested, 2),
            events.OrderByDescending(e => e.OccurredAt).Take(10).Select(MapEvent).ToList());
    }

    public async Task<IReadOnlyList<PlannerSummaryDto>> GetPlannersAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        EnsureInsightsAndPlanningAccess(tenantId);
        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var plannerIds = await FilterEvents(tenantId, fromUtc, toUtc, InsightsDomains.RoutePlanning, null, null, null, null, null)
            .Where(e => e.UserId.HasValue)
            .Select(e => e.UserId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var summaries = new List<PlannerSummaryDto>();
        foreach (var plannerId in plannerIds)
        {
            var summary = await GetPlannerSummaryAsync(tenantId, plannerId, from, to, ct);
            if (summary is not null)
                summaries.Add(summary);
        }

        return summaries.OrderByDescending(p => p.PlansRequested).ToList();
    }

    public async Task<AnalyticsReportBundleDto?> GetReportAsync(
        string reportType,
        Guid tenantId,
        Guid subjectId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        EnsureInsightsAccess(tenantId);

        return reportType switch
        {
            "driver.performance" => await BuildDriverReportAsync(tenantId, subjectId, from, to, ct),
            "vehicle.performance" => await BuildVehicleReportAsync(tenantId, subjectId, from, to, ct),
            "customer.service" => await BuildCustomerReportAsync(tenantId, subjectId, from, to, ct),
            "planner.activity" => await BuildPlannerReportAsync(tenantId, subjectId, from, to, ct),
            "tenant.operations_digest" => await BuildTenantReportAsync(tenantId, from, to, ct),
            _ => null
        };
    }

    public async Task<IReadOnlyList<OperationalEventDto>> GetEventsAsync(
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        string? domain = null,
        Guid? driverId = null,
        Guid? vehicleId = null,
        Guid? customerId = null,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        EnsureInsightsAccess(tenantId);
        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var events = await FilterEvents(tenantId, fromUtc, toUtc, domain, driverId, vehicleId, customerId, userId, null)
            .OrderByDescending(e => e.OccurredAt)
            .Take(500)
            .ToListAsync(ct);
        return events.Select(MapEvent).ToList();
    }

    private async Task<AnalyticsReportBundleDto?> BuildDriverReportAsync(Guid tenantId, Guid driverId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var summary = await GetDriverSummaryAsync(tenantId, driverId, from, to, ct);
        if (summary is null) return null;
        return ToReportBundle("driver.performance", tenantId, from, to, summary.SubjectType, summary.SubjectId, summary.Label, null, summary);
    }

    private async Task<AnalyticsReportBundleDto?> BuildVehicleReportAsync(Guid tenantId, Guid vehicleId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var summary = await GetVehicleSummaryAsync(tenantId, vehicleId, from, to, ct);
        if (summary is null) return null;
        return ToReportBundle("vehicle.performance", tenantId, from, to, summary.SubjectType, summary.SubjectId, summary.Label, null, summary);
    }

    private async Task<AnalyticsReportBundleDto?> BuildCustomerReportAsync(Guid tenantId, Guid customerId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var summary = await GetCustomerSummaryAsync(tenantId, customerId, from, to, ct);
        if (summary is null) return null;
        return ToReportBundle("customer.service", tenantId, from, to, summary.SubjectType, summary.SubjectId, summary.Label, null, summary);
    }

    private async Task<AnalyticsReportBundleDto?> BuildPlannerReportAsync(Guid tenantId, Guid userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        EnsureInsightsAndPlanningAccess(tenantId);
        var summary = await GetPlannerSummaryAsync(tenantId, userId, from, to, ct);
        if (summary is null) return null;
        var kpis = new Dictionary<string, object?>
        {
            ["plansRequested"] = summary.PlansRequested,
            ["plansAccepted"] = summary.PlansAccepted,
            ["plansDiscarded"] = summary.PlansDiscarded,
            ["ordersPlanned"] = summary.OrdersPlanned,
            ["acceptRate"] = summary.AcceptRate
        };
        var narrative = $"{summary.DisplayName} requested {summary.PlansRequested} plans and accepted {summary.PlansAccepted} ({summary.AcceptRate:P0}) between {from} and {to}.";
        return new AnalyticsReportBundleDto(
            "1.0",
            "planner.activity",
            tenantId,
            from,
            to,
            new ReportSubjectDto("user", userId, summary.DisplayName, null),
            narrative,
            kpis,
            summary.NotableEvents,
            null);
    }

    private async Task<AnalyticsReportBundleDto?> BuildTenantReportAsync(Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var digest = await GetTenantDigestAsync(tenantId, from, to, ct);
        var kpis = new Dictionary<string, object?>
        {
            ["ordersDelivered"] = digest.Delivery.OrdersDelivered,
            ["ordersFailed"] = digest.Delivery.OrdersFailed,
            ["stopsCompleted"] = digest.Delivery.StopsCompleted,
            ["routesCompleted"] = digest.Delivery.RoutesCompleted
        };
        if (digest.Planning is not null)
        {
            kpis["plansRequested"] = digest.Planning.PlansRequested;
            kpis["plansAccepted"] = digest.Planning.PlansAccepted;
            kpis["planAcceptRate"] = digest.Planning.AcceptRate;
        }

        var narrative = $"Tenant completed {digest.Delivery.OrdersDelivered} deliveries with {digest.Delivery.StopsCompleted} stops between {from} and {to}.";
        return new AnalyticsReportBundleDto(
            "1.0",
            "tenant.operations_digest",
            tenantId,
            from,
            to,
            new ReportSubjectDto("tenant", tenantId, "Tenant", null),
            narrative,
            kpis,
            [],
            null);
    }

    private async Task<SubjectSummaryDto> BuildSubjectSummaryAsync(
        string subjectType,
        Guid subjectId,
        string label,
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        Guid? driverId,
        Guid? vehicleId,
        Guid? customerId,
        Guid? userId,
        CancellationToken ct)
    {
        var (fromUtc, toUtc) = ToUtcRange(from, to);
        var events = await FilterEvents(tenantId, fromUtc, toUtc, InsightsDomains.Delivery, driverId, vehicleId, customerId, userId, null)
            .ToListAsync(ct);

        return new SubjectSummaryDto(
            subjectType,
            subjectId,
            label,
            from,
            to,
            BuildKpis(events),
            events.OrderByDescending(e => e.OccurredAt).Take(10).Select(MapEvent).ToList());
    }

    private static AnalyticsReportBundleDto ToReportBundle(
        string reportType,
        Guid tenantId,
        DateOnly from,
        DateOnly to,
        string subjectType,
        Guid subjectId,
        string label,
        string? role,
        SubjectSummaryDto summary)
    {
        var kpis = new Dictionary<string, object?>
        {
            ["eventCount"] = summary.Kpis.EventCount,
            ["ordersDelivered"] = summary.Kpis.OrdersDelivered,
            ["ordersFailed"] = summary.Kpis.OrdersFailed,
            ["stopsCompleted"] = summary.Kpis.StopsCompleted,
            ["routesCompleted"] = summary.Kpis.RoutesCompleted,
            ["onTimeRate"] = summary.Kpis.OnTimeRate,
            ["avgDelayMinutes"] = summary.Kpis.AvgDelayMinutes
        };

        var narrative = $"{label} recorded {summary.Kpis.OrdersDelivered} delivered orders and {summary.Kpis.StopsCompleted} completed stops from {from} to {to}.";

        return new AnalyticsReportBundleDto(
            "1.0",
            reportType,
            tenantId,
            from,
            to,
            new ReportSubjectDto(subjectType, subjectId, label, role),
            narrative,
            kpis,
            summary.NotableEvents,
            null);
    }

    private static InsightsKpiDto BuildKpis(IReadOnlyList<OperationalEvent> events) =>
        new(
            events.Count,
            events.Count(e => e.EventType == DeliveryEventTypes.OrderDelivered),
            events.Count(e => e.EventType == DeliveryEventTypes.OrderFailed),
            events.Count(e => e.EventType == DeliveryEventTypes.StopCompleted),
            events.Count(e => e.EventType == DeliveryEventTypes.RouteCompleted),
            null,
            null);

    private IQueryable<OperationalEvent> FilterEvents(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        string? domain,
        Guid? driverId,
        Guid? vehicleId,
        Guid? customerId,
        Guid? userId,
        Guid? planRunId)
    {
        var query = db.OperationalEvents.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.OccurredAt >= fromUtc && e.OccurredAt <= toUtc);

        if (!string.IsNullOrWhiteSpace(domain))
            query = query.Where(e => e.Domain == domain);
        if (driverId.HasValue)
            query = query.Where(e => e.DriverId == driverId);
        if (vehicleId.HasValue)
            query = query.Where(e => e.VehicleId == vehicleId);
        if (customerId.HasValue)
        {
            query = query.Where(e =>
                e.CustomerId == customerId
                || (e.OrderId != null
                    && db.DeliveryOrders.Any(o => o.Id == e.OrderId && o.CustomerId == customerId))
                || (e.StopId != null
                    && db.DeliveryRouteStops.Any(s =>
                        s.Id == e.StopId
                        && s.DeliveryOrderId != null
                        && db.DeliveryOrders.Any(o => o.Id == s.DeliveryOrderId && o.CustomerId == customerId))));
        }
        if (userId.HasValue)
            query = query.Where(e => e.UserId == userId);
        if (planRunId.HasValue)
            query = query.Where(e => e.PlanRunId == planRunId);

        return query;
    }

    private void EnsureInsightsAccess(Guid tenantId)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights);
        currentUser.EnsureTenantAccess(tenantId);
    }

    private void EnsureInsightsAndPlanningAccess(Guid tenantId)
    {
        currentUser.EnsureModules(ProductModule.Delivery, ProductModule.Insights, ProductModule.RoutePlanning);
        currentUser.EnsureTenantAccess(tenantId);
    }

    private static (DateTime FromUtc, DateTime ToUtc) ToUtcRange(DateOnly from, DateOnly to) =>
        (from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc));

    private static OperationalEventDto MapEvent(OperationalEvent e) =>
        new(
            e.Id,
            e.OccurredAt,
            e.Domain,
            e.EventType,
            e.OrderId,
            e.RouteId,
            e.StopId,
            e.VehicleId,
            e.DriverId,
            e.CustomerId,
            e.UserId,
            e.PlanRunId,
            e.DriverLabel,
            e.VehicleLabel,
            e.CustomerLabel,
            e.PlannerLabel,
            e.Narrative,
            e.MetricsJson,
            e.ContextJson);

    private static int ReadIntMetric(OperationalEvent e, string key)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(e.MetricsJson);
            if (doc.RootElement.TryGetProperty(key, out var value) && value.TryGetInt32(out var n))
                return n;
        }
        catch
        {
            // ignore malformed metrics
        }

        return 0;
    }
}
