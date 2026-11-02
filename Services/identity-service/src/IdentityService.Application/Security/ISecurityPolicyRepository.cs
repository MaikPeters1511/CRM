using IdentityService.Domain.Security;

namespace IdentityService.Application.Security;

/// <summary>Zugriff auf die org-weite Sicherheitsrichtlinie (Singleton-Zeile, identity-device-trust.openspec.md).</summary>
public interface ISecurityPolicyRepository
{
    Task<SecurityPolicy> GetAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
