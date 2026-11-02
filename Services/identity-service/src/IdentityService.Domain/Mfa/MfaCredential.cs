namespace IdentityService.Domain.Mfa;

/// <summary>
/// TOTP-Credential (RFC 6238) eines Nutzers (identity-mfa-totp.openspec.md).
/// KNOWN SIMPLIFICATION (docs/adr/0004, Abschnitt 2): <see cref="SecretBase32"/> liegt unverschluesselt
/// (Base32-Klartext) in identity_db - Data-Protection-Verschluesselung ist verbindliche Folgeaufgabe.
/// </summary>
public sealed class MfaCredential
{
    public MfaCredential(Guid id, Guid userId, string secretBase32)
    {
        Id = id;
        UserId = userId;
        SecretBase32 = secretBase32;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private MfaCredential()
    {
        SecretBase32 = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string SecretBase32 { get; private set; }

    public bool Verified { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public void MarkVerified()
    {
        Verified = true;
        VerifiedAtUtc = DateTimeOffset.UtcNow;
    }
}
