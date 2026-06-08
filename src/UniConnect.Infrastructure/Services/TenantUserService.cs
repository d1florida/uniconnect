using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Identity;
using UniConnect.Infrastructure.Services.Insights;
using UniConnect.Tenant;
using UniConnect.Tenant.DTOs;
using UniConnect.Tenant.Enums;
using UniConnect.Tenant.Interfaces;
using TenantEntity = UniConnect.Tenant.Entities.Tenant;

namespace UniConnect.Infrastructure.Services;

public class TenantUserService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser) : ITenantUserService
{
    public async Task<IReadOnlyList<TenantUserDto>> GetMyUsersAsync(CancellationToken ct = default)
    {
        var tenant = await LoadTenantForAdminAsync(ct);
        var users = await userManager.Users.AsNoTracking()
            .Where(u => u.TenantId == tenant.Id)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var userIds = users.Select(u => u.Id).ToList();
        var linkedDrivers = await db.Drivers.AsNoTracking()
            .Where(d => d.TenantId == tenant.Id && d.UserId != null && userIds.Contains(d.UserId.Value))
            .ToDictionaryAsync(d => d.UserId!.Value, d => d, ct);

        return users.Select(u =>
        {
            linkedDrivers.TryGetValue(u.Id, out var driver);
            return ToDto(u, tenant.Modules, driver?.Id, driver?.DisplayName);
        }).ToList();
    }

    public async Task<TenantUserDto> CreateMyUserAsync(CreateTenantUserRequest request, CancellationToken ct = default)
    {
        var tenant = await LoadTenantForAdminAsync(ct);

        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        var password = request.Password.Trim();
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.");
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required.");
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ArgumentException("A user with this email already exists.");
        var moduleAccess = request.Role == TenantRole.Driver
            ? DriverUserProvisioning.DriverModuleAccess(tenant.Modules)
            : ValidateModuleAccess(request.ModuleAccess, tenant.Modules);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            TenantId = tenant.Id,
            TenantRole = request.Role,
            ModuleAccess = moduleAccess,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new ArgumentException(FormatIdentityErrors(result.Errors));

        Guid? linkedDriverId = null;
        string? linkedDriverName = null;
        if (request.Role == TenantRole.Driver)
        {
            var driver = await DriverUserProvisioning.EnsureDriverForUserAsync(db, tenant.Id, user, ct);
            linkedDriverId = driver.Id;
            linkedDriverName = driver.DisplayName;
        }

        return ToDto(user, tenant.Modules, linkedDriverId, linkedDriverName);
    }

    public async Task<TenantUserDto> UpdateMyUserAsync(Guid userId, UpdateTenantUserRequest request, CancellationToken ct = default)
    {
        var tenant = await LoadTenantForAdminAsync(ct);
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenant.Id, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (request.DisplayName is not null)
        {
            var displayName = request.DisplayName.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name cannot be empty.");
            user.DisplayName = displayName;
        }

        if (request.Email is not null)
        {
            var newEmail = request.Email.Trim();
            if (string.IsNullOrWhiteSpace(newEmail))
                throw new ArgumentException("Email cannot be empty.");

            if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await userManager.FindByEmailAsync(newEmail);
                if (existing is not null && existing.Id != user.Id)
                    throw new ArgumentException("A user with this email already exists.");

                user.Email = newEmail;
                user.UserName = newEmail;
                user.NormalizedEmail = userManager.NormalizeEmail(newEmail);
                user.NormalizedUserName = userManager.NormalizeName(newEmail);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, token, request.NewPassword.Trim());
            if (!resetResult.Succeeded)
                throw new ArgumentException(FormatIdentityErrors(resetResult.Errors));
        }

        var previousRole = user.TenantRole;
        if (request.Role.HasValue)
        {
            if (request.Role.Value == TenantRole.Admin && user.TenantRole == TenantRole.Driver)
                throw new ArgumentException("Promote the user to Operator before granting admin access.");
            user.TenantRole = request.Role.Value;
        }

        if (user.TenantRole == TenantRole.Driver)
            user.ModuleAccess = DriverUserProvisioning.DriverModuleAccess(tenant.Modules);
        else if (request.ModuleAccess is not null)
            user.ModuleAccess = ValidateModuleAccess(request.ModuleAccess, tenant.Modules);

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        if (user.IsActive && user.ModuleAccess == ProductModule.None)
            throw new ArgumentException("Active users must have at least one product module.");

        if (user is { TenantRole: TenantRole.Admin, IsActive: true })
        {
            // still an active admin — ok
        }
        else
            await EnsureRemainingAdminAsync(tenant.Id, user.Id, ct);

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new ArgumentException(FormatIdentityErrors(updateResult.Errors));

        if (user.TenantRole == TenantRole.Driver)
            await DriverUserProvisioning.EnsureDriverForUserAsync(db, tenant.Id, user, ct);
        else if (previousRole == TenantRole.Driver)
            await DriverUserProvisioning.UnlinkUserAsync(db, user.Id, ct);

        var linkedDriver = await db.Drivers.AsNoTracking()
            .FirstOrDefaultAsync(d => d.TenantId == tenant.Id && d.UserId == user.Id, ct);

        return ToDto(user, tenant.Modules, linkedDriver?.Id, linkedDriver?.DisplayName);
    }

    public async Task DeleteMyUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tenant = await LoadTenantForAdminAsync(ct);
        if (currentUser.UserId == userId)
            throw new InvalidOperationException("You cannot remove your own account.");

        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenant.Id, ct)
            ?? throw new InvalidOperationException("User not found.");

        await EnsureRemainingAdminAsync(tenant.Id, userId, ct);
        await DriverUserProvisioning.UnlinkUserAsync(db, user.Id, ct);

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(FormatIdentityErrors(result.Errors));
    }

    private async Task<TenantEntity> LoadTenantForAdminAsync(CancellationToken ct)
    {
        currentUser.EnsureTenantAdmin();
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant operator account required.");
        return await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new InvalidOperationException("Tenant not found.");
    }

    private static ProductModule ValidateModuleAccess(IReadOnlyList<ProductModule> requested, ProductModule tenantModules)
    {
        var access = ProductModuleHelper.Combine(requested);
        if (access == ProductModule.None)
            throw new ArgumentException("Select at least one product module.");

        var invalid = access & ~tenantModules;
        if (invalid != ProductModule.None)
            throw new ArgumentException("Module access cannot exceed tenant-enabled products.");

        return access;
    }

    private async Task EnsureRemainingAdminAsync(Guid tenantId, Guid excludedUserId, CancellationToken ct)
    {
        var otherActiveAdmins = await userManager.Users.AsNoTracking()
            .CountAsync(
                u => u.TenantId == tenantId
                     && u.Id != excludedUserId
                     && u.TenantRole == TenantRole.Admin
                     && u.IsActive,
                ct);

        if (otherActiveAdmins == 0)
            throw new InvalidOperationException("The tenant must have at least one active administrator.");
    }

    private static TenantUserDto ToDto(
        ApplicationUser user,
        ProductModule tenantModules,
        Guid? linkedDriverId = null,
        string? linkedDriverName = null)
    {
        var effective = ProductModuleHelper.EffectiveModules(user.ModuleAccess, tenantModules);
        return new TenantUserDto(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.TenantRole,
            ProductModuleHelper.Expand(effective),
            user.IsActive,
            linkedDriverId,
            linkedDriverName);
    }

    private static string FormatIdentityErrors(IEnumerable<IdentityError> errors) =>
        string.Join(" ", errors.Select(e => e.Description));
}
