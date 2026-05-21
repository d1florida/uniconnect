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
        string? fleetName = null;
        Domain.Enums.FleetType? fleetType = null;
        if (user.FleetId.HasValue)
        {
            var fleet = await db.Fleets.AsNoTracking().FirstOrDefaultAsync(f => f.Id == user.FleetId, ct);
            fleetName = fleet?.Name;
            fleetType = fleet?.FleetType;
        }

        var isAdmin = await userManager.IsInRoleAsync(user, "PlatformAdmin");
        return new UserProfileDto(user.Id, user.Email!, user.DisplayName, user.FleetId, fleetName, fleetType, isAdmin);
    }

    private string GenerateToken(ApplicationUser user, UserProfileDto profile, DateTime expires)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
        };

        if (user.FleetId.HasValue)
        {
            claims.Add(new Claim("fleet_id", user.FleetId.Value.ToString()));
            if (profile.FleetType.HasValue)
                claims.Add(new Claim("fleet_type", profile.FleetType.Value.ToString()));
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
