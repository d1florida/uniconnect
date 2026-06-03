using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniConnect.Application;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Identity;
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

        return users.Select(u => ToDto(u, tenant.Modules)).ToList();
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

        var moduleAccess = ValidateModuleAccess(request.ModuleAccess, tenant.Modules);

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

        return ToDto(user, tenant.Modules);
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

        if (request.Role.HasValue)
            user.TenantRole = request.Role.Value;

        if (request.ModuleAccess is not null)
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

        return ToDto(user, tenant.Modules);
    }

    public async Task DeleteMyUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tenant = await LoadTenantForAdminAsync(ct);
        if (currentUser.UserId == userId)
            throw new InvalidOperationException("You cannot remove your own account.");

        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenant.Id, ct)
            ?? throw new InvalidOperationException("User not found.");

        await EnsureRemainingAdminAsync(tenant.Id, userId, ct);

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

    private static TenantUserDto ToDto(ApplicationUser user, ProductModule tenantModules)
    {
        var effective = ProductModuleHelper.EffectiveModules(user.ModuleAccess, tenantModules);
        return new TenantUserDto(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.TenantRole,
            ProductModuleHelper.Expand(effective),
            user.IsActive);
    }

    private static string FormatIdentityErrors(IEnumerable<IdentityError> errors) =>
        string.Join(" ", errors.Select(e => e.Description));
}
