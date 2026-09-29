using IdentityService.Application.Auth;
using IdentityService.Domain.Users;
using Microsoft.AspNetCore.Identity;
// PasswordHasher<T> lebt im Paket Microsoft.Extensions.Identity.Core, Namespace Microsoft.AspNetCore.Identity.

namespace IdentityService.Infrastructure.Users;

/// <summary>
/// Nutzt ASP.NET Core Identity's PasswordHasher&lt;T&gt; (PBKDF2) fuer Hashing/Verify,
/// wie von CLAUDE.md/Tech-Stack ("Microsoft Identity") vorgegeben - ohne die volle
/// ASP.NET Core Identity-Infrastruktur (UserManager/EF-Store) einzufuehren, da fuer
/// zwei Seed-Nutzer keine Persistenzschicht benoetigt wird.
/// </summary>
public sealed class IdentityPasswordVerifier : IPasswordVerifier
{
    private readonly PasswordHasher<User> _hasher = new();

    public bool Verify(User user, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);
}
