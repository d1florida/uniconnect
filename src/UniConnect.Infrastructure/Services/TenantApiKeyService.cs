using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UniConnect.Application.Interfaces;
using UniConnect.Infrastructure.Data;
using UniConnect.Tenant.DTOs;
using UniConnect.Tenant.Entities;
using UniConnect.Tenant.Interfaces;

namespace UniConnect.Infrastructure.Services;

public class TenantApiKeyService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IConfiguration configuration) : ITenantApiKeyService
{
    private const string KeyPrefixLive = "uc_live_";
    private const int PrefixLength = 16;
    private const int SecretLength = 32;

    public async Task<IReadOnlyList<TenantApiKeyDto>> GetMyKeysAsync(CancellationToken ct = default)
    {
        var tenantId = RequireOperatorTenantId();
        return await db.TenantApiKeys.AsNoTracking()
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => MapKey(k))
            .ToListAsync(ct);
    }

    public async Task<CreateTenantApiKeyResponse> CreateMyKeyAsync(CreateTenantApiKeyRequest request, CancellationToken ct = default)
    {
        EnsureApiKeysEnabled();
        var tenantId = RequireOperatorTenantId();
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Key name is required.");

        var secret = GenerateSecret();
        var entity = new TenantApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            KeyPrefix = secret[..PrefixLength],
            KeyHash = HashSecret(secret),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = currentUser.UserId
        };

        db.TenantApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);
        return new CreateTenantApiKeyResponse(MapKey(entity), secret);
    }

    public async Task RevokeMyKeyAsync(Guid keyId, CancellationToken ct = default)
    {
        var tenantId = RequireOperatorTenantId();
        var key = await db.TenantApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("API key not found.");
        if (key.RevokedAt is not null) return;

        key.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private Guid RequireOperatorTenantId()
    {
        currentUser.EnsureTenantAdmin();
        if (currentUser.IsApiKeyAuth)
            throw new InvalidOperationException("API keys cannot manage other API keys.");
        var tenantId = currentUser.TenantId
            ?? throw new InvalidOperationException("Tenant operator account required.");
        return tenantId;
    }

    private void EnsureApiKeysEnabled()
    {
        if (!configuration.GetValue("ApiKeys:Enabled", true))
            throw new InvalidOperationException("API keys are disabled by the platform.");
    }

    private static string GenerateSecret()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = RandomNumberGenerator.GetBytes(SecretLength - KeyPrefixLive.Length);
        var builder = new StringBuilder(KeyPrefixLive, SecretLength);
        foreach (var b in bytes)
            builder.Append(alphabet[b % alphabet.Length]);
        return builder.ToString();
    }

    internal static string HashSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static TenantApiKeyDto MapKey(TenantApiKey key) =>
        new(
            key.Id,
            key.TenantId,
            key.Name,
            key.KeyPrefix,
            key.CreatedAt,
            key.LastUsedAt,
            key.RevokedAt,
            key.RevokedAt is null);
}
