using IdentityService.Domain.Users;

namespace IdentityService.Application.Auth;

/// <summary>
/// Abstraktion ueber Microsoft.AspNetCore.Identity's PasswordHasher&lt;T&gt;, damit der
/// Application-Layer nicht direkt von der Infrastructure/Identity-Bibliothek abhaengt.
/// </summary>
public interface IPasswordVerifier
{
    bool Verify(User user, string providedPassword);

    string Hash(User user, string password);
}
