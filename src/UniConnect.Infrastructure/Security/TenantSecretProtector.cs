using Microsoft.AspNetCore.DataProtection;
using UniConnect.Application.Interfaces;

namespace UniConnect.Infrastructure.Security;

public sealed class TenantSecretProtector(IDataProtectionProvider dataProtection) : ITenantSecretProtector
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("UniConnect.TenantSecrets.v1");

    public string Protect(string plaintext) =>
        _protector.Protect(plaintext);

    public string Unprotect(string protectedPayload) =>
        _protector.Unprotect(protectedPayload);

    public bool TryUnprotect(string protectedPayload, out string? plaintext)
    {
        try
        {
            plaintext = _protector.Unprotect(protectedPayload);
            return true;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            plaintext = null;
            return false;
        }
    }

    public string CreateHint(string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return string.Empty;
        return secret.Length <= 4 ? "••••" : $"…{secret[^4..]}";
    }
}
