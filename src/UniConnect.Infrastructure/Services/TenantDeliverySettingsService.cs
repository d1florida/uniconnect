using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Tenant.DTOs;
using UniConnect.Tenant.Entities;
using UniConnect.Tenant.Interfaces;

namespace UniConnect.Infrastructure.Services;

public sealed class TenantDeliverySettingsService(
    AppDbContext db,
    ICurrentUserService currentUser) : ITenantDeliverySettingsService
{
    public Task<TenantDeliverySettingsDto> GetMySettingsAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantAdminId();
        return GetSettingsAsync(tenantId, ct);
    }

    public async Task<TenantDeliverySettingsDto> UpdateMySettingsAsync(
        UpdateTenantDeliverySettingsRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantAdminId();
        var entity = await GetOrCreateAsync(tenantId, ct);

        entity.AllowMultipleRoutesPerDriverPerDay = request.AllowMultipleRoutesPerDriverPerDay;
        if (!string.IsNullOrWhiteSpace(request.TimeZoneId))
            entity.TimeZoneId = request.TimeZoneId.Trim();

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<TenantDeliverySettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entity = await GetOrCreateAsync(tenantId, ct);
        return Map(entity);
    }

    private async Task<TenantDeliverySettings> GetOrCreateAsync(Guid tenantId, CancellationToken ct)
    {
        var entity = await db.TenantDeliverySettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        if (entity is not null)
            return entity;

        entity = new TenantDeliverySettings { TenantId = tenantId, UpdatedAt = DateTime.UtcNow };
        db.TenantDeliverySettings.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    private static TenantDeliverySettingsDto Map(TenantDeliverySettings entity) =>
        new(entity.AllowMultipleRoutesPerDriverPerDay, entity.TimeZoneId, entity.UpdatedAt);

    private Guid RequireTenantAdminId()
    {
        currentUser.EnsureTenantAdmin();
        return currentUser.TenantId ?? throw new InvalidOperationException("Tenant context required.");
    }
}
