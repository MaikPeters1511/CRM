namespace IdentityService.Domain.Security;

/// <summary>
/// Org-weite Sicherheitsrichtlinie (identity-device-trust.openspec.md): genau eine Zeile in
/// identity_db (Singleton, feste Id <see cref="SingletonId"/>). Admin-verwaltet ueber
/// <c>PUT /api/admin/security-policy</c>.
/// </summary>
public sealed class SecurityPolicy
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public SecurityPolicy()
    {
        Id = SingletonId;
    }

    public Guid Id { get; private set; }

    public bool RequireMfa { get; private set; }

    public int PasswordMinLength { get; private set; } = 8;

    public int PasswordExpiryDays { get; private set; } = 90;

    public int MaxFailedLoginAttempts { get; private set; } = 5;

    public int LockoutDurationMinutes { get; private set; } = 15;

    public void Update(
        bool requireMfa,
        int passwordMinLength,
        int passwordExpiryDays,
        int maxFailedLoginAttempts,
        int lockoutDurationMinutes)
    {
        if (passwordMinLength < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(passwordMinLength), "Mindestlaenge darf OWASP-Empfehlung (8) nicht unterschreiten.");
        }

        if (maxFailedLoginAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFailedLoginAttempts), "Mindestens 1 erlaubter Versuch vor Sperrung.");
        }

        RequireMfa = requireMfa;
        PasswordMinLength = passwordMinLength;
        PasswordExpiryDays = passwordExpiryDays;
        MaxFailedLoginAttempts = maxFailedLoginAttempts;
        LockoutDurationMinutes = lockoutDurationMinutes;
    }
}
