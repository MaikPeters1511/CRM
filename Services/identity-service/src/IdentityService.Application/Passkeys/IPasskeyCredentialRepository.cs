using IdentityService.Domain.Passkeys;

namespace IdentityService.Application.Passkeys;

public interface IPasskeyCredentialRepository
{
    Task<PasskeyCredential?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PasskeyCredential>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<PasskeyCredential?> FindByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(PasskeyCredential credential, CancellationToken cancellationToken);

    void Remove(PasskeyCredential credential);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
