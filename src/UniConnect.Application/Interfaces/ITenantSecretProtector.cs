namespace UniConnect.Application.Interfaces;

public interface ITenantSecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedPayload);
    bool TryUnprotect(string protectedPayload, out string? plaintext);
    string CreateHint(string secret);
}
