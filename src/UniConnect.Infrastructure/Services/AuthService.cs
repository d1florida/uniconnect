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

        if (!await userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var profile = await BuildProfileAsync(user, ct);
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
        IReadOnlyList<UniConnect.Tenant.Enums.ProductModule> modules = [];
        if (user.TenantId.HasValue)
        {
            var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);
            tenantName = tenant?.Name;
            if (tenant is not null)
                modules = ProductModuleHelper.Expand(tenant.Modules);
        }

        var isAdmin = await userManager.IsInRoleAsync(user, "PlatformAdmin");
        return new UserProfileDto(user.Id, user.Email!, user.DisplayName, user.TenantId, tenantName, modules, isAdmin);
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
            var moduleFlags = ProductModuleHelper.Combine(profile.Modules);
            if (moduleFlags != UniConnect.Tenant.Enums.ProductModule.None)
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
