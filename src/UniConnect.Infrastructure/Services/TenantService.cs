using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Identity;
using UniConnect.Tenant;
using UniConnect.Tenant.DTOs;
using TenantEntity = UniConnect.Tenant.Entities.Tenant;
using UniConnect.Tenant.Enums;
using UniConnect.Tenant.Interfaces;

namespace UniConnect.Infrastructure.Services;

public class TenantService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser) : ITenantService
{
    public async Task<IReadOnlyList<TenantDto>> GetTenantsAsync(ProductModule? module = null, CancellationToken ct = default)
    {
        var query = db.Tenants.AsNoTracking();
        if (!currentUser.IsPlatformAdmin && currentUser.TenantId.HasValue)
            query = query.Where(t => t.Id == currentUser.TenantId.Value);
        else if (module.HasValue && module.Value != ProductModule.None)
            query = query.Where(t => (t.Modules & module.Value) == module.Value);

        var tenants = await query.OrderBy(t => t.Name).ToListAsync(ct);
        var adminEmails = await GetAdminEmailsAsync(tenants.Select(t => t.Id), ct);
        return tenants.Select(t => ToDto(t, adminEmails.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TenantDto?> GetTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (!currentUser.IsPlatformAdmin)
        {
            if (!currentUser.TenantId.HasValue || currentUser.TenantId.Value != tenantId)
                throw new ForbiddenException("You do not have access to this tenant.");
        }

        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return null;

        var adminEmail = await GetAdminEmailAsync(tenantId, ct);
        return ToDto(tenant, adminEmail);
    }

    public async Task<TenantDto> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsPlatformAdmin)
            throw new ForbiddenException("Only platform administrators can create tenants.");

        var modules = ProductModuleHelper.Combine(request.Modules);
        if (modules == ProductModule.None)
            throw new ArgumentException("Select at least one product module.");

        var adminEmail = request.AdminEmail.Trim();
        var adminPassword = request.AdminPassword.Trim();
        if (string.IsNullOrWhiteSpace(adminEmail))
            throw new ArgumentException("Tenant administrator email is required.");
        if (string.IsNullOrWhiteSpace(adminPassword))
            throw new ArgumentException("Tenant administrator password is required.");
        if (await userManager.FindByEmailAsync(adminEmail) is not null)
            throw new ArgumentException("A user with this administrator email already exists.");

        var tenant = new TenantEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug.ToLowerInvariant(),
            Modules = modules,
            ContactName = request.ContactName?.Trim() ?? string.Empty,
            ContactEmail = request.ContactEmail?.Trim() ?? string.Empty,
            ContactPhone = request.ContactPhone?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        var displayName = !string.IsNullOrWhiteSpace(tenant.ContactName)
            ? tenant.ContactName
            : $"{tenant.Name} Admin";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = adminEmail,
            Email = adminEmail,
            DisplayName = displayName,
            TenantId = tenant.Id,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, adminPassword);
        if (!createResult.Succeeded)
            throw new ArgumentException(FormatIdentityErrors(createResult.Errors));

        await tx.CommitAsync(ct);
        return ToDto(tenant, adminEmail);
    }

    public async Task<TenantDto> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsPlatformAdmin)
            throw new ForbiddenException("Only platform administrators can update tenants.");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Tenant not found.");

        if (!string.IsNullOrWhiteSpace(request.Name))
            tenant.Name = request.Name;
        if (!string.IsNullOrWhiteSpace(request.Slug))
            tenant.Slug = request.Slug.ToLowerInvariant();
        if (request.Modules is not null)
        {
            var modules = ProductModuleHelper.Combine(request.Modules);
            if (modules == ProductModule.None)
                throw new ArgumentException("Select at least one product module.");
            tenant.Modules = modules;
        }
        if (request.ContactName is not null)
            tenant.ContactName = request.ContactName.Trim();
        if (request.ContactEmail is not null)
            tenant.ContactEmail = request.ContactEmail.Trim();
        if (request.ContactPhone is not null)
            tenant.ContactPhone = request.ContactPhone.Trim();

        var admin = await GetAdminUserAsync(tenantId, ct);
        var adminEmail = admin?.Email;
        var adminPassword = request.AdminPassword?.Trim();
        var adminCreated = false;

        if (request.AdminEmail is not null)
        {
            var newEmail = request.AdminEmail.Trim();
            if (string.IsNullOrWhiteSpace(newEmail))
                throw new ArgumentException("Tenant administrator email cannot be empty.");

            if (admin is null)
            {
                if (string.IsNullOrWhiteSpace(adminPassword))
                    throw new ArgumentException("Set a password when assigning a new tenant administrator.");

                if (await userManager.FindByEmailAsync(newEmail) is not null)
                    throw new ArgumentException("A user with this administrator email already exists.");

                var displayName = !string.IsNullOrWhiteSpace(tenant.ContactName)
                    ? tenant.ContactName
                    : $"{tenant.Name} Admin";
                admin = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = newEmail,
                    Email = newEmail,
                    DisplayName = displayName,
                    TenantId = tenant.Id,
                    EmailConfirmed = true
                };
                var createResult = await userManager.CreateAsync(admin, adminPassword);
                if (!createResult.Succeeded)
                    throw new ArgumentException(FormatIdentityErrors(createResult.Errors));
                adminCreated = true;
            }
            else if (!string.Equals(admin.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await userManager.FindByEmailAsync(newEmail);
                if (existing is not null && existing.Id != admin.Id)
                    throw new ArgumentException("A user with this administrator email already exists.");

                admin.Email = newEmail;
                admin.UserName = newEmail;
                admin.NormalizedEmail = userManager.NormalizeEmail(newEmail);
                admin.NormalizedUserName = userManager.NormalizeName(newEmail);
                var updateResult = await userManager.UpdateAsync(admin);
                if (!updateResult.Succeeded)
                    throw new ArgumentException(string.Join(" ", updateResult.Errors.Select(e => e.Description)));
            }

            adminEmail = newEmail;
        }

        if (!adminCreated && !string.IsNullOrWhiteSpace(adminPassword))
        {
            admin ??= await GetAdminUserAsync(tenantId, ct)
                ?? throw new ArgumentException("No tenant administrator exists. Set an administrator email first.");

            var token = await userManager.GeneratePasswordResetTokenAsync(admin);
            var resetResult = await userManager.ResetPasswordAsync(admin, token, adminPassword);
            if (!resetResult.Succeeded)
                throw new ArgumentException(FormatIdentityErrors(resetResult.Errors));
        }

        await db.SaveChangesAsync(ct);
        adminEmail ??= (await GetAdminUserAsync(tenantId, ct))?.Email;
        return ToDto(tenant, adminEmail);
    }

    public async Task DeleteTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (!currentUser.IsPlatformAdmin)
            throw new ForbiddenException("Only platform administrators can delete tenants.");

        if (tenantId == DbSeed.GeneralTenantId || tenantId == DbSeed.AvTenantId || tenantId == DbSeed.DeliveryTenantId)
            throw new InvalidOperationException("Demo tenants cannot be deleted.");

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Tenant not found.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var routes = await db.DeliveryRoutes.Where(r => r.TenantId == tenantId).ToListAsync(ct);
        db.DeliveryRoutes.RemoveRange(routes);

        var orders = await db.DeliveryOrders.Where(o => o.TenantId == tenantId).ToListAsync(ct);
        db.DeliveryOrders.RemoveRange(orders);

        var accounts = await db.BusinessAccounts.Where(a => a.TenantId == tenantId).ToListAsync(ct);
        db.BusinessAccounts.RemoveRange(accounts);

        var vehicleIds = await db.Vehicles.Where(v => v.TenantId == tenantId).Select(v => v.Id).ToListAsync(ct);
        if (vehicleIds.Count > 0)
        {
            var profiles = await db.RoboTaxiProfiles.Where(p => vehicleIds.Contains(p.VehicleId)).ToListAsync(ct);
            db.RoboTaxiProfiles.RemoveRange(profiles);
            var vehicles = await db.Vehicles.Where(v => v.TenantId == tenantId).ToListAsync(ct);
            db.Vehicles.RemoveRange(vehicles);
        }

        var users = await userManager.Users.Where(u => u.TenantId == tenantId).ToListAsync(ct);
        foreach (var user in users)
        {
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new InvalidOperationException(FormatIdentityErrors(result.Errors));
        }

        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<TenantDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsPlatformAdmin)
            throw new ForbiddenException("Only platform administrators can view the tenant dashboard.");

        var tenants = await db.Tenants.AsNoTracking().Select(t => t.Modules).ToListAsync(ct);
        return new TenantDashboardDto(
            tenants.Count,
            tenants.Count(m => ProductModuleHelper.HasModule(m, ProductModule.General)),
            tenants.Count(m => ProductModuleHelper.HasModule(m, ProductModule.RoboTaxi)),
            tenants.Count(m => ProductModuleHelper.HasModule(m, ProductModule.Delivery)));
    }

    private async Task<ApplicationUser?> GetAdminUserAsync(Guid tenantId, CancellationToken ct) =>
        await userManager.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.Email)
            .FirstOrDefaultAsync(ct);

    private async Task<string?> GetAdminEmailAsync(Guid tenantId, CancellationToken ct) =>
        (await GetAdminUserAsync(tenantId, ct))?.Email;

    private async Task<Dictionary<Guid, string?>> GetAdminEmailsAsync(IEnumerable<Guid> tenantIds, CancellationToken ct)
    {
        var ids = tenantIds.ToList();
        if (ids.Count == 0) return [];

        var users = await userManager.Users
            .AsNoTracking()
            .Where(u => u.TenantId.HasValue && ids.Contains(u.TenantId.Value))
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        return users
            .GroupBy(u => u.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Email);
    }

    private static string FormatIdentityErrors(IEnumerable<IdentityError> errors) =>
        string.Join(" ", errors.Select(e => e.Description));

    private static TenantDto ToDto(TenantEntity tenant, string? adminEmail) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            ProductModuleHelper.Expand(tenant.Modules),
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            adminEmail,
            tenant.CreatedAt);
}
