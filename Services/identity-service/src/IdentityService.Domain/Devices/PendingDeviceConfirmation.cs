namespace IdentityService.Domain.Devices;

/// <summary>
/// Offene Geraete-Bestaetigung (identity-device-trust.openspec.md): wird angelegt, wenn ein Login
/// von einem unbekannten/gesperrten Geraet erfolgt, und per 6-stelligem Code (gehasht gespeichert)
/// ueber <c>IDeviceConfirmationNotifier</c> bestaetigt. Bewusst in identity_db persistiert (nicht
/// In-Memory wie WebAuthn-Challenges), da E-Mail-Versand-Latenz und Prozess-Neustarts ueberlebt
/// werden muessen (docs/adr/0004, Abschnitt 3).
/// </summary>
public sealed class PendingDeviceConfirmation
{
    public PendingDeviceConfirmation(
        Guid id,
        Guid userId,
        string fingerprintHash,
        string userAgent,
        string lastIpRange,
        string codeHash,
        DateTimeOffset expiresAtUtc)
    {
        Id = id;
        UserId = userId;
        FingerprintHash = fingerprintHash;
        UserAgent = userAgent;
        LastIpRange = lastIpRange;
        CodeHash = codeHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private PendingDeviceConfirmation()
    {
        FingerprintHash = string.Empty;
        UserAgent = string.Empty;
        LastIpRange = string.Empty;
        CodeHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string FingerprintHash { get; private set; }

    public string UserAgent { get; private set; }

    public string LastIpRange { get; private set; }

    public string CodeHash { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc > ExpiresAtUtc;

    public bool IsConfirmed => ConfirmedAtUtc is not null;

    public void MarkConfirmed() => ConfirmedAtUtc = DateTimeOffset.UtcNow;
}
