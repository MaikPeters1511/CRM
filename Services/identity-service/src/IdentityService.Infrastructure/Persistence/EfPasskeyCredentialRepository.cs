using IdentityService.Application.Passkeys;
using IdentityService.Domain.Passkeys;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class EfPasskeyCredentialRepository(IdentityDbContext dbContext) : IPasskeyCredentialRepository
{
    public Task<PasskeyCredential?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken) =>
        dbContext.PasskeyCredentials.FirstOrDefaultAsync(p => p.CredentialId.SequenceEqual(credentialId), cancellationToken);

    public async Task<IReadOnlyList<PasskeyCredential>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.PasskeyCredentials.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(cancellationToken);

    public Task<PasskeyCredential?> FindByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        dbContext.PasskeyCredentials.FirstOrDefaultAsync(p => p.UserId == userId && p.Id == id, cancellationToken);

    public async Task AddAsync(PasskeyCredential credential, CancellationToken cancellationToken) =>
        await dbContext.PasskeyCredentials.AddAsync(credential, cancellationToken);

    public void Remove(PasskeyCredential credential) => dbContext.PasskeyCredentials.Remove(credential);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
