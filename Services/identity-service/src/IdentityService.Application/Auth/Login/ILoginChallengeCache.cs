namespace IdentityService.Application.Auth.Login;

/// <summary>
/// Kurzlebiger Zwischenspeicher fuer den MFA-Login-Zwischenstatus (docs/adr/0004, Abschnitt 5):
/// nach erfolgreicher Passwort-/Device-Trust-Pruefung wird ein Challenge-Token ausgestellt, das
/// fuer <c>POST /api/auth/login/mfa</c> auf den Nutzer verweist, ohne das Passwort erneut zu uebertragen.
/// Implementiert in Infrastructure via IMemoryCache mit TTL - bewusst NICHT in identity_db
/// persistiert (gleiches Muster wie WebAuthn-Challenges, docs/adr/0004 Abschnitt 3).
/// </summary>
public interface ILoginChallengeCache
{
    string IssueToken(Guid userId);

    Guid? TryConsumeToken(string token);
}
