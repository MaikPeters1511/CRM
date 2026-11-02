using IdentityService.Application.Security;
using IdentityService.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

/// <summary>Stellt sicher, dass immer genau eine Zeile existiert (Singleton-Policy, docs/adr/0004).</summary>
public sealed class EfSecurityPolicyRepository(IdentityDbContext dbContext) : ISecurityPolicyRepository
{
    public async Task<SecurityPolicy> GetAsync(CancellationToken cancellationToken)
    {
        var policy = await dbContext.SecurityPolicies.FirstOrDefaultAsync(cancellationToken);
        if (policy is not null)
        {
            return policy;
        }

        policy = new SecurityPolicy();
        await dbContext.SecurityPolicies.AddAsync(policy, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
