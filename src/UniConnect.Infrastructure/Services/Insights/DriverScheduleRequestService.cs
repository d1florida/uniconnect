using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Identity;
using UniConnect.Insights.DTOs;
using UniConnect.Insights.Entities;
using UniConnect.Insights.Enums;
using UniConnect.Insights.Interfaces;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services.Insights;

public class DriverScheduleRequestService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser,
    IDriverDirectory driverDirectory) : IDriverScheduleRequestService
{
    public async Task<IReadOnlyList<DriverScheduleRequestDto>> GetRequestsAsync(
        Guid tenantId,
        string? status = null,
        CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var query = db.DriverScheduleRequests.AsNoTracking()
            .Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<DriverScheduleRequestStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }

        if (currentUser.IsDriver && currentUser.DriverId.HasValue)
            query = query.Where(r => r.DriverId == currentUser.DriverId.Value);

        var rows = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return await MapRequestsAsync(rows, ct);
    }

    public async Task<DriverScheduleRequestDto> CreateRequestAsync(
        Guid tenantId,
        CreateDriverScheduleRequestRequest request,
        CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        if (!currentUser.UserId.HasValue)
            throw new InvalidOperationException("User context is required.");

        if (request.ToDate < request.FromDate)
            throw new ArgumentException("End date must be on or after start date.");
        if (request.FromDate.AddDays(90) < request.ToDate)
            throw new ArgumentException("Request range cannot exceed 90 days.");

        var driverId = request.DriverId;
        if (currentUser.IsDriver)
        {
            if (!currentUser.DriverId.HasValue)
                throw new InvalidOperationException("Your account is not linked to a driver profile.");
            driverId = currentUser.DriverId.Value;
        }

        var driver = await db.Drivers.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Driver not found.");

        if (!driver.IsActive)
            throw new InvalidOperationException("Cannot request time off for an inactive driver.");

        var requestType = ParseRequestType(request.RequestType);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        var row = new DriverScheduleRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DriverId = driverId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            RequestType = requestType,
            Note = note,
            Status = DriverScheduleRequestStatus.Pending,
            RequestedByUserId = currentUser.UserId.Value,
            CreatedAt = DateTime.UtcNow
        };

        db.DriverScheduleRequests.Add(row);
        await db.SaveChangesAsync(ct);
        return (await MapRequestsAsync([row], ct))[0];
    }

    public async Task<DriverScheduleRequestDto> ApproveRequestAsync(
        Guid tenantId,
        Guid requestId,
        ReviewDriverScheduleRequestRequest? review = null,
        CancellationToken ct = default)
    {
        EnsureAdminAccess(tenantId);
        var row = await db.DriverScheduleRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Schedule request not found.");

        if (row.Status != DriverScheduleRequestStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be approved.");

        var typeLabel = FormatRequestType(row.RequestType);
        var note = string.IsNullOrWhiteSpace(row.Note)
            ? typeLabel
            : $"{typeLabel}: {row.Note}";

        await driverDirectory.BulkUpsertScheduleExceptionsAsync(
            tenantId,
            row.DriverId,
            new BulkUpsertDriverScheduleExceptionRequest(
                row.FromDate,
                row.ToDate,
                IsWorking: false,
                Note: note),
            ct);

        row.Status = DriverScheduleRequestStatus.Approved;
        row.ReviewedByUserId = currentUser.UserId;
        row.ReviewedAt = DateTime.UtcNow;
        row.ReviewNote = string.IsNullOrWhiteSpace(review?.ReviewNote) ? null : review.ReviewNote.Trim();
        await db.SaveChangesAsync(ct);

        return (await MapRequestsAsync([row], ct))[0];
    }

    public async Task<DriverScheduleRequestDto> DenyRequestAsync(
        Guid tenantId,
        Guid requestId,
        ReviewDriverScheduleRequestRequest? review = null,
        CancellationToken ct = default)
    {
        EnsureAdminAccess(tenantId);
        var row = await db.DriverScheduleRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Schedule request not found.");

        if (row.Status != DriverScheduleRequestStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be denied.");

        row.Status = DriverScheduleRequestStatus.Denied;
        row.ReviewedByUserId = currentUser.UserId;
        row.ReviewedAt = DateTime.UtcNow;
        row.ReviewNote = string.IsNullOrWhiteSpace(review?.ReviewNote) ? null : review.ReviewNote.Trim();
        await db.SaveChangesAsync(ct);

        return (await MapRequestsAsync([row], ct))[0];
    }

    public async Task CancelRequestAsync(Guid tenantId, Guid requestId, CancellationToken ct = default)
    {
        EnsureReadAccess(tenantId);
        var row = await db.DriverScheduleRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Schedule request not found.");

        if (row.Status != DriverScheduleRequestStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be cancelled.");

        if (!currentUser.IsTenantAdmin && !currentUser.IsPlatformAdmin)
        {
            if (row.RequestedByUserId != currentUser.UserId)
                throw new UnauthorizedAccessException("You can only cancel your own pending requests.");
            if (currentUser.IsDriver && currentUser.DriverId.HasValue && row.DriverId != currentUser.DriverId.Value)
                throw new UnauthorizedAccessException("You can only cancel your own pending requests.");
        }

        row.Status = DriverScheduleRequestStatus.Cancelled;
        await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<DriverScheduleRequestDto>> MapRequestsAsync(
        IReadOnlyList<DriverScheduleRequest> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
            return [];

        var driverIds = rows.Select(r => r.DriverId).Distinct().ToList();
        var drivers = await db.Drivers.AsNoTracking()
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.DisplayName, ct);

        var userIds = rows
            .SelectMany(r => new[] { r.RequestedByUserId, r.ReviewedByUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var userNames = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await userManager.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return rows.Select(r => new DriverScheduleRequestDto(
            r.Id,
            r.DriverId,
            drivers.GetValueOrDefault(r.DriverId, "Unknown"),
            r.FromDate,
            r.ToDate,
            FormatRequestType(r.RequestType),
            r.Status.ToString(),
            r.Note,
            userNames.GetValueOrDefault(r.RequestedByUserId, "Unknown"),
            r.CreatedAt,
            r.ReviewedByUserId.HasValue
                ? userNames.GetValueOrDefault(r.ReviewedByUserId.Value, "Unknown")
                : null,
            r.ReviewedAt,
            r.ReviewNote)).ToList();
    }

    private static DriverScheduleRequestType ParseRequestType(string value) =>
        Enum.TryParse<DriverScheduleRequestType>(value, true, out var parsed)
            ? parsed
            : throw new ArgumentException("Request type must be Pto, Sick, Training, or Other.");

    private static string FormatRequestType(DriverScheduleRequestType type) => type switch
    {
        DriverScheduleRequestType.Pto => "PTO",
        DriverScheduleRequestType.Sick => "Sick",
        DriverScheduleRequestType.Training => "Training",
        _ => "Other"
    };

    private void EnsureReadAccess(Guid tenantId)
    {
        currentUser.EnsureModule(ProductModule.Delivery);
        currentUser.EnsureTenantAccess(tenantId);
    }

    private void EnsureAdminAccess(Guid tenantId)
    {
        EnsureReadAccess(tenantId);
        currentUser.EnsureTenantAdmin();
    }
}
