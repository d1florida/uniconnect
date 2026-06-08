using System.Text.Json;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;
using UniConnect.Insights.Interfaces;
using UniConnect.Infrastructure.Data;

namespace UniConnect.Infrastructure.Services.Insights;

public class OperationalEventRecorder(AppDbContext db) : IOperationalEventRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task RecordAsync(RecordOperationalEventRequest request, CancellationToken ct = default)
    {
        var entry = new OperationalEvent
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            OccurredAt = DateTime.UtcNow,
            Domain = request.Domain,
            EventType = request.EventType,
            OrderId = request.OrderId,
            RouteId = request.RouteId,
            StopId = request.StopId,
            VehicleId = request.VehicleId,
            DriverId = request.DriverId,
            CustomerId = request.CustomerId,
            UserId = request.UserId,
            PlanRunId = request.PlanRunId,
            DriverLabel = request.DriverLabel,
            VehicleLabel = request.VehicleLabel,
            CustomerLabel = request.CustomerLabel,
            PlannerLabel = request.PlannerLabel,
            Narrative = request.Narrative,
            MetricsJson = JsonSerializer.Serialize(request.Metrics ?? new Dictionary<string, object?>(), JsonOptions),
            ContextJson = JsonSerializer.Serialize(request.Context ?? new Dictionary<string, object?>(), JsonOptions)
        };

        db.OperationalEvents.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
