using IdentityService.Domain.Security;

namespace IdentityService.Application.Security;

public sealed class GetSecurityPolicyHandler(ISecurityPolicyRepository repository)
{
    public Task<SecurityPolicy> HandleAsync(CancellationToken cancellationToken) => repository.GetAsync(cancellationToken);
}
