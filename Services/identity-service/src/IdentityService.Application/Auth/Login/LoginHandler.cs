using IdentityService.Application.Audit;
using IdentityService.Application.Devices;
using IdentityService.Application.Mfa;
using IdentityService.Application.Security;
using IdentityService.Domain.Audit;
using IdentityService.Domain.Devices;

namespace IdentityService.Application.Auth.Login;

/// <summary>
/// Use-Case-Handler fuer den Login-Flow. Reihenfolge der Gates verbindlich nach docs/adr/0004,
/// Abschnitt 5: Lockout -> Passwort -> Device-Trust -> MFA. Jeder Zwischenstatus stellt KEIN
/// Access-Token aus.
/// </summary>
public sealed class LoginHandler(
    IUserRepository userRepository,
    IPasswordVerifier passwordVerifier,
    ITokenGenerator tokenGenerator,
    ISecurityPolicyRepository securityPolicyRepository,
    IKnownDeviceRepository knownDeviceRepository,
    IPendingDeviceConfirmationRepository pendingDeviceConfirmationRepository,
    IDeviceConfirmationNotifier deviceConfirmationNotifier,
    IMfaCredentialRepository mfaCredentialRepository,
    ILoginChallengeCache loginChallengeCache,
    IAuditLogger auditLogger,
    TimeProvider timeProvider)
{
    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var user = await userRepository.FindByEmailAsync(command.Email, cancellationToken);

        // Lockout-Gate zuerst, noch vor der Passwort-Pruefung: verhindert Rueckschluesse auf
        // Passwort-Korrektheit bei bereits gesperrtem Account (docs/adr/0004, Abschnitt 5).
        if (user is not null && user.IsLockedOut(now))
        {
            return new LoginResult(LoginStatus.Locked);
        }

        // Bewusst identisches Verhalten (kein Unterschied in Timing/Antwort) fuer
        // "Nutzer nicht gefunden" und "Passwort falsch", um User-Enumeration (OWASP A07) zu vermeiden.
        if (user is null || !passwordVerifier.Verify(user, command.Password))
        {
            if (user is not null)
            {
                var policy = await securityPolicyRepository.GetAsync(cancellationToken);
                user.RecordFailedLoginAttempt(
                    policy.MaxFailedLoginAttempts, TimeSpan.FromMinutes(policy.LockoutDurationMinutes), now);
                await userRepository.SaveChangesAsync(cancellationToken);

                await auditLogger.LogAsync(
                    AuditEventType.LoginFailed, user.Id, user.Id, "Login fehlgeschlagen (falsches Passwort).",
                    command.IpAddress, cancellationToken);
            }

            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        return await ContinueAfterPasswordVerifiedAsync(user, command.UserAgent, command.IpAddress, now, cancellationToken);
    }

    /// <summary>Gemeinsamer Fortsetzungspunkt fuer passwortbasierten und Passkey-Login (docs/adr/0004, Abschnitt 5, Punkt 5).</summary>
    public async Task<LoginResult> ContinueAfterPasswordVerifiedAsync(
        Domain.Users.User user, string userAgent, string ipAddress, DateTimeOffset now, CancellationToken cancellationToken)
    {
        user.ResetFailedLoginAttempts();
        await userRepository.SaveChangesAsync(cancellationToken);

        var fingerprintHash = DeviceFingerprint.Compute(userAgent, ipAddress);
        var device = await knownDeviceRepository.FindAsync(user.Id, fingerprintHash, cancellationToken);

        if (device is null || device.IsRevoked)
        {
            var code = GenerateSixDigitCode();
            var pending = new PendingDeviceConfirmation(
                Guid.NewGuid(), user.Id, fingerprintHash, userAgent, DeviceFingerprint.IpBucket(ipAddress),
                ConfirmDeviceHandler.HashCode(code), now.AddMinutes(15));

            await pendingDeviceConfirmationRepository.AddAsync(pending, cancellationToken);
            await pendingDeviceConfirmationRepository.SaveChangesAsync(cancellationToken);
            await deviceConfirmationNotifier.NotifyAsync(user.Email, code, cancellationToken);

            await auditLogger.LogAsync(
                AuditEventType.DeviceConfirmationRequested, user.Id, user.Id,
                "Unbekanntes Geraet erkannt, Bestaetigungscode versendet.", ipAddress, cancellationToken);

            return new LoginResult(LoginStatus.DeviceConfirmationRequired, PendingDeviceConfirmationId: pending.Id);
        }

        device.RecordSeen(userAgent, DeviceFingerprint.IpBucket(ipAddress));
        await knownDeviceRepository.SaveChangesAsync(cancellationToken);

        var securityPolicy = await securityPolicyRepository.GetAsync(cancellationToken);
        var mfaCredential = await mfaCredentialRepository.FindByUserIdAsync(user.Id, cancellationToken);
        var mfaRequired = user.MfaEnabled && mfaCredential is { Verified: true };

        if (mfaRequired)
        {
            var challengeToken = loginChallengeCache.IssueToken(user.Id);
            return new LoginResult(LoginStatus.MfaRequired, MfaChallengeToken: challengeToken);
        }

        return await IssueSuccessAsync(user, ipAddress, cancellationToken);
    }

    public async Task<LoginResult> IssueSuccessAsync(Domain.Users.User user, string ipAddress, CancellationToken cancellationToken)
    {
        var token = tokenGenerator.GenerateToken(user, out var expiresAtUtc);

        await auditLogger.LogAsync(
            AuditEventType.LoginSucceeded, user.Id, user.Id, "Login erfolgreich.", ipAddress, cancellationToken);

        return new LoginResult(
            LoginStatus.Success, token, expiresAtUtc, user.Id.ToString(), user.Role.ToString(), user.DisplayName);
    }

    private static string GenerateSixDigitCode() =>
        System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
