using IdentityService.Application.Mfa;
using OtpNet;

namespace IdentityService.Infrastructure.Mfa;

/// <summary>RFC 6238 (TOTP) via OtpNet (docs/adr/0004, Abschnitt 2) - keine Eigenimplementierung des Algorithmus.</summary>
public sealed class OtpNetTotpProvider : ITotpProvider
{
    private const int SecretByteLength = 20;

    public string GenerateSecret()
    {
        var secretBytes = KeyGeneration.GenerateRandomKey(SecretByteLength);
        return Base32Encoding.ToString(secretBytes);
    }

    public string BuildOtpAuthUri(string secretBase32, string accountEmail, string issuer)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountEmail}");
        var issuerEncoded = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secretBase32}&issuer={issuerEncoded}&algorithm=SHA1&digits=6&period=30";
    }

    public bool ValidateCode(string secretBase32, string code)
    {
        var secretBytes = Base32Encoding.ToBytes(secretBase32);
        var totp = new Totp(secretBytes);
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
