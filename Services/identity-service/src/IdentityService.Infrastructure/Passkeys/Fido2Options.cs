namespace IdentityService.Infrastructure.Passkeys;

/// <summary>RP-ID/Origin-Konfiguration je Umgebung (docs/adr/0004, Abschnitt 3) - kein Hardcoding.</summary>
public sealed class Fido2Options
{
    public const string SectionName = "WebAuthn";

    public string ServerDomain { get; set; } = "localhost";

    public string ServerName { get; set; } = "CRM";

    public string[] Origins { get; set; } = ["http://localhost:4200"];
}
