using System.Security.Cryptography;
using System.Text;

namespace IdentityService.Application.Devices;

/// <summary>
/// Pragmatische Geraete-Fingerprint-Heuristik (identity-device-trust.openspec.md, docs/adr/0004
/// Abschnitt 4): Hash aus User-Agent + IP-/24-Subnetz. Bewusst kein robustes Device-Binding
/// (siehe ADR "Sicherheitsannahmen fuer Review") - reines SHA-256-Hashing, keine Eigenkryptografie.
/// </summary>
public static class DeviceFingerprint
{
    public static string Compute(string userAgent, string ipAddress)
    {
        var ipBucket = IpBucket(ipAddress);
        var bytes = Encoding.UTF8.GetBytes($"{userAgent}|{ipBucket}");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>Reduziert eine IPv4-Adresse auf ihr /24-Subnetz; IPv6 und ungueltige Werte werden unveraendert durchgereicht.</summary>
    public static string IpBucket(string ipAddress)
    {
        var parts = ipAddress.Split('.');
        return parts.Length == 4 ? $"{parts[0]}.{parts[1]}.{parts[2]}.0/24" : ipAddress;
    }
}
