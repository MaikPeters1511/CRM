using IdentityService.Infrastructure.Mfa;
using OtpNet;

namespace IdentityService.Tests.Infrastructure;

/// <summary>Verifiziert, dass OtpNetTotpProvider tatsaechlich RFC-6238-konforme Codes akzeptiert/ablehnt (docs/adr/0004, Abschnitt 2).</summary>
public class OtpNetTotpProviderTests
{
    [Test]
    public async Task GenerateSecret_ReturnsValidBase32String()
    {
        var provider = new OtpNetTotpProvider();

        var secret = provider.GenerateSecret();

        await Assert.That(secret).IsNotNullOrEmpty();
        await Assert.That(() => Base32Encoding.ToBytes(secret)).ThrowsNothing();
    }

    [Test]
    public async Task ValidateCode_WithCurrentCode_ReturnsTrue()
    {
        var provider = new OtpNetTotpProvider();
        var secret = provider.GenerateSecret();
        var currentCode = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        var isValid = provider.ValidateCode(secret, currentCode);

        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task ValidateCode_WithWrongCode_ReturnsFalse()
    {
        var provider = new OtpNetTotpProvider();
        var secret = provider.GenerateSecret();

        var isValid = provider.ValidateCode(secret, "000000");

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task BuildOtpAuthUri_ContainsSecretIssuerAndAccount()
    {
        var provider = new OtpNetTotpProvider();

        var uri = provider.BuildOtpAuthUri("ABC123", "admin@crm.local", "CRM");

        await Assert.That(uri).StartsWith("otpauth://totp/");
        await Assert.That(uri).Contains("secret=ABC123");
        await Assert.That(uri).Contains("issuer=CRM");
    }
}
