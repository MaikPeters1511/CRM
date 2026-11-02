using IdentityService.Domain.Mfa;

namespace IdentityService.Application.Mfa;

public interface IMfaCredentialRepository
{
    Task<MfaCredential?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(MfaCredential credential, CancellationToken cancellationToken);

    void Remove(MfaCredential credential);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
