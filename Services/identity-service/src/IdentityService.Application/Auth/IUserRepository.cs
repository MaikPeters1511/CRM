using IdentityService.Domain.Users;

namespace IdentityService.Application.Auth;

/// <summary>
/// Repository-Interface (Application-Layer definiert den Contract, Infrastructure-Layer
/// implementiert ihn) fuer den Zugriff auf die geseedeten Test-Nutzer.
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}
