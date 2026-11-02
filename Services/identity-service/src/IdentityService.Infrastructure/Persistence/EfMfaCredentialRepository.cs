using IdentityService.Application.Mfa;
using IdentityService.Domain.Mfa;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class EfMfaCredentialRepository(IdentityDbContext dbContext) : IMfaCredentialRepository
{
    public Task<MfaCredential?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.MfaCredentials.FirstOrDefaultAsync(m => m.UserId == userId, cancellationToken);

    public async Task AddAsync(MfaCredential credential, CancellationToken cancellationToken) =>
        await dbContext.MfaCredentials.AddAsync(credential, cancellationToken);

    public void Remove(MfaCredential credential) => dbContext.MfaCredentials.Remove(credential);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
