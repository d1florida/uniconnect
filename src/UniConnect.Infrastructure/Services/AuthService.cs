using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using UniConnect.Application.DTOs;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Infrastructure.Identity;
using UniConnect.Tenant;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    IConfiguration configuration) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("This account has been disabled.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var profile = await BuildProfileAsync(user, ct);
        if (user.TenantId.HasValue && profile.Modules.Count == 0)
            throw new UnauthorizedAccessException("This account has no product access. Contact your tenant administrator.");

        var expires = DateTime.UtcNow.AddHours(configuration.GetValue("Jwt:ExpireHours", 24));
        var token = GenerateToken(user, profile, expires);
        return new LoginResponse(token, expires, profile);
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");
        return await BuildProfileAsync(user, ct);
    }

    private async Task<UserProfileDto> BuildProfileAsync(ApplicationUser user, CancellationToken ct)
    {
        string? tenantName = null;
        IReadOnlyList<ProductModule> modules = [];
        var isPlatformAdmin = await userManager.IsInRoleAsync(user, "PlatformAdmin");

        if (user.TenantId.HasValue)
        {
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);
            tenantName = tenant?.Name;
            if (tenant is not null)
            {
                var effective = ProductModuleHelper.EffectiveModules(user.ModuleAccess, tenant.Modules);
                modules = ProductModuleHelper.Expand(effective);
            }
        }

        var isTenantAdmin = user.TenantId.HasValue && user.TenantRole == TenantRole.Admin;
        return new UserProfileDto(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.TenantId,
            tenantName,
            modules,
            isPlatformAdmin,
            user.TenantId.HasValue ? user.TenantRole : null,
            isTenantAdmin);
    }

    private string GenerateToken(ApplicationUser user, UserProfileDto profile, DateTime expires)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
        };

        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));
            claims.Add(new Claim("tenant_role", user.TenantRole.ToString()));
            var moduleFlags = ProductModuleHelper.Combine(profile.Modules);
            if (moduleFlags != ProductModule.None)
                claims.Add(new Claim("product_modules", ((int)moduleFlags).ToString()));
        }

        if (profile.IsPlatformAdmin)
            claims.Add(new Claim("role", "PlatformAdmin"));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
