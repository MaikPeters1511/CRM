using IdentityService.Domain.Users;

namespace IdentityService.Application.Auth;

public interface ITokenGenerator
{
    /// <summary>
    /// Erzeugt ein signiertes JWT mit den Claims "sub"/NameIdentifier (User.Id) und
    /// ClaimTypes.Role (User.Role), die von customer-service/employee-service fuer
    /// RBAC- und Ownership-Checks ausgewertet werden.
    /// </summary>
    string GenerateToken(User user, out DateTimeOffset expiresAtUtc);
}
