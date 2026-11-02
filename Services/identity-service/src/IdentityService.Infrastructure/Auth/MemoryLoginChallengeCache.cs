using IdentityService.Application.Auth.Login;
using Microsoft.Extensions.Caching.Memory;

namespace IdentityService.Infrastructure.Auth;

/// <summary>
/// In-Memory-Implementierung des MFA-Login-Zwischenstatus (docs/adr/0004, Abschnitt 5) - bewusst
/// nicht in identity_db, gleiches Muster wie WebAuthn-Challenges (Abschnitt 3).
/// </summary>
public sealed class MemoryLoginChallengeCache(IMemoryCache cache) : ILoginChallengeCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public string IssueToken(Guid userId)
    {
        var token = Guid.NewGuid().ToString("N");
        cache.Set(CacheKey(token), userId, Ttl);
        return token;
    }

    public Guid? TryConsumeToken(string token)
    {
        if (!cache.TryGetValue(CacheKey(token), out Guid userId))
        {
            return null;
        }

        cache.Remove(CacheKey(token));
        return userId;
    }

    private static string CacheKey(string token) => $"login-mfa-challenge:{token}";
}
