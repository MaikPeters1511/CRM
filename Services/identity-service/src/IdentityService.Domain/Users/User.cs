namespace IdentityService.Domain.Users;

/// <summary>
/// Identity-Aggregate-Root. Seit identity-mfa-totp.openspec.md persistiert in <c>identity_db</c>
/// (EF Core), aber weiterhin ohne Infrastruktur-Abhaengigkeiten im Domain-Layer: EF Core mappt
/// private Setter ueber Reflection, es gibt kein EF-Core-Package-Reference in diesem Projekt.
/// </summary>
public sealed class User
{
    public User(Guid id, string email, string passwordHash, UserRole role, string displayName)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        DisplayName = displayName;
    }

    // EF-Core-Konstruktor (private, parameterlos) - alle Properties haben private Setter.
    private User()
    {
        Email = string.Empty;
        PasswordHash = string.Empty;
        DisplayName = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public string DisplayName { get; private set; }

    /// <summary>Eigene MFA-Einstellung (identity-mfa-totp.openspec.md). Unabhaengig von <see cref="SecurityPolicy"/>.RequireMfa.</summary>
    public bool MfaEnabled { get; private set; }

    /// <summary>Zaehler fuer fehlgeschlagene Login-Versuche in Folge (identity-device-trust.openspec.md, Account-Lockout).</summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>Gesetzt, solange der Account wegen zu vieler Fehlversuche gesperrt ist.</summary>
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset PasswordChangedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        PasswordChangedAtUtc = DateTimeOffset.UtcNow;
    }

    public void EnableMfa() => MfaEnabled = true;

    public void DisableMfa() => MfaEnabled = false;

    public bool IsLockedOut(DateTimeOffset nowUtc) => LockedUntilUtc is { } until && until > nowUtc;

    public void RecordFailedLoginAttempt(int maxAttempts, TimeSpan lockoutDuration, DateTimeOffset nowUtc)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
        {
            LockedUntilUtc = nowUtc.Add(lockoutDuration);
        }
    }

    public void ResetFailedLoginAttempts()
    {
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }
}
