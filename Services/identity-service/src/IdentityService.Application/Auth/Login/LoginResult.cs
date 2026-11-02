namespace IdentityService.Application.Auth.Login;

/// <summary>
/// Reihenfolge der Login-Gates (docs/adr/0004, Abschnitt 5): Lockout -> Passwort -> Device-Trust -> MFA.
/// <see cref="Locked"/>/<see cref="DeviceConfirmationRequired"/>/<see cref="MfaRequired"/> stellen zu
/// keinem Zeitpunkt ein Access-Token aus.
/// </summary>
public enum LoginStatus
{
    Success,
    InvalidCredentials,
    Locked,
    DeviceConfirmationRequired,
    MfaRequired,
}

public sealed record LoginResult(
    LoginStatus Status,
    string? AccessToken = null,
    DateTimeOffset? ExpiresAtUtc = null,
    string? UserId = null,
    string? Role = null,
    string? DisplayName = null,
    /// <summary>Kurzlebiges Challenge-Token fuer <c>POST /api/auth/login/mfa</c> (nur bei <see cref="LoginStatus.MfaRequired"/>).</summary>
    string? MfaChallengeToken = null,
    /// <summary>Id der offenen Geraete-Bestaetigung fuer <c>POST /api/auth/devices/confirm</c> (nur bei <see cref="LoginStatus.DeviceConfirmationRequired"/>).</summary>
    Guid? PendingDeviceConfirmationId = null);
