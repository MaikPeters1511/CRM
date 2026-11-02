namespace IdentityService.Domain.Devices;

/// <summary>
/// Bekanntes, bereits bestaetigtes Geraet eines Nutzers (identity-device-trust.openspec.md).
/// Enthaelt bewusst NUR den gehashten Fingerprint (kein Rohtext-Tracking, docs/adr/0004 Abschnitt 4) -
/// <see cref="UserAgent"/>/<see cref="LastIpRange"/> dienen ausschliesslich der Admin-Anzeige und
/// werden bei jedem erneuten Login desselben Geraets ueberschrieben, nicht historisiert.
/// </summary>
public sealed class KnownDevice
{
    public KnownDevice(Guid id, Guid userId, string fingerprintHash, string userAgent, string lastIpRange)
    {
        Id = id;
        UserId = userId;
        FingerprintHash = fingerprintHash;
        UserAgent = userAgent;
        LastIpRange = lastIpRange;
        FirstSeenAtUtc = DateTimeOffset.UtcNow;
        LastSeenAtUtc = FirstSeenAtUtc;
    }

    private KnownDevice()
    {
        FingerprintHash = string.Empty;
        UserAgent = string.Empty;
        LastIpRange = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string FingerprintHash { get; private set; }

    public string UserAgent { get; private set; }

    public string LastIpRange { get; private set; }

    public bool IsRevoked { get; private set; }

    public DateTimeOffset FirstSeenAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public void RecordSeen(string userAgent, string lastIpRange)
    {
        UserAgent = userAgent;
        LastIpRange = lastIpRange;
        LastSeenAtUtc = DateTimeOffset.UtcNow;
    }

    public void Revoke() => IsRevoked = true;

    public void Unrevoke() => IsRevoked = false;
}
