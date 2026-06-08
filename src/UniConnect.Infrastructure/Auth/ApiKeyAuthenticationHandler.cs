using System.Text.Encodings.Web;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniConnect.Infrastructure.Data;
using UniConnect.Tenant;
using UniConnect.Tenant.Enums;

namespace UniConnect.Infrastructure.Auth;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    private const int PrefixLength = 16;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!configuration.GetValue("ApiKeys:Enabled", true))
            return AuthenticateResult.Fail("API keys are disabled.");

        if (!Request.Headers.TryGetValue("X-Api-Key", out var headerValues))
            return AuthenticateResult.NoResult();

        var providedKey = headerValues.ToString().Trim();
        if (providedKey.Length <= PrefixLength)
            return AuthenticateResult.Fail("Invalid API key.");

        var prefix = providedKey[..PrefixLength];
        var key = await db.TenantApiKeys
            .AsNoTracking()
            .Include(k => k.Tenant)
            .FirstOrDefaultAsync(k => k.KeyPrefix == prefix && k.RevokedAt == null, Context.RequestAborted);

        if (key is null || !VerifyHash(providedKey, key.KeyHash))
            return AuthenticateResult.Fail("Invalid API key.");

        await db.TenantApiKeys
            .Where(k => k.Id == key.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(k => k.LastUsedAt, DateTime.UtcNow),
                Context.RequestAborted);

        var moduleFlags = key.Tenant.Modules;
        var claims = new List<Claim>
        {
            new("auth_type", "ApiKey"),
            new("tenant_id", key.TenantId.ToString()),
            new("api_key_id", key.Id.ToString()),
            new("product_modules", ((int)moduleFlags).ToString())
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    private static bool VerifyHash(string providedKey, string storedHash)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(providedKey)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash),
            Encoding.UTF8.GetBytes(storedHash));
    }
}
