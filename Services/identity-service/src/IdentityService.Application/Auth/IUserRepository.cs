using IdentityService.Domain.Users;

namespace IdentityService.Application.Auth;

/// <summary>
/// Repository-Interface (Application-Layer definiert den Contract, Infrastructure-Layer
/// implementiert ihn) fuer den Zugriff auf Nutzer in identity_db.
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
