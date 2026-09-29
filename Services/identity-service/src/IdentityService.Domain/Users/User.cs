namespace IdentityService.Domain.Users;

/// <summary>
/// Minimaler Identity-Aggregate-Root fuer diesen Entwicklungsstand.
/// Kein vollwertiger ASP.NET Core Identity User (kein EF Core Persistence-Model im Domain-Layer,
/// gemaess Clean-Architecture-Regel: keine Infrastruktur-Abhaengigkeiten im Domain-Layer).
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

    public Guid Id { get; }

    public string Email { get; }

    public string PasswordHash { get; }

    public UserRole Role { get; }

    public string DisplayName { get; }
}
