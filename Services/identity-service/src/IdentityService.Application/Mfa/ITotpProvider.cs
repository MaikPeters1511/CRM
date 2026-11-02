namespace IdentityService.Application.Mfa;

/// <summary>
/// Kapselt RFC 6238 (TOTP) hinter einem Application-Interface, implementiert in Infrastructure
/// mittels OtpNet (docs/adr/0004, Abschnitt 2) - keine Eigenimplementierung des Algorithmus.
/// </summary>
public interface ITotpProvider
{
    /// <summary>Erzeugt ein neues Base32-codiertes Secret.</summary>
    string GenerateSecret();

    /// <summary>Liefert die otpauth://-URI fuer QR-Code-Rendering im Frontend (dieser Service rendert keinen QR-Code selbst).</summary>
    string BuildOtpAuthUri(string secretBase32, string accountEmail, string issuer);

    /// <summary>Prueft den vom Nutzer eingegebenen 6-stelligen Code gegen das Secret (mit Zeitfenster-Toleranz).</summary>
    bool ValidateCode(string secretBase32, string code);
}
