using IdentityService.Application.Devices;
using IdentityService.Domain.Devices;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class EfPendingDeviceConfirmationRepository(IdentityDbContext dbContext) : IPendingDeviceConfirmationRepository
{
    public Task<PendingDeviceConfirmation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.PendingDeviceConfirmations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddAsync(PendingDeviceConfirmation confirmation, CancellationToken cancellationToken) =>
        await dbContext.PendingDeviceConfirmations.AddAsync(confirmation, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
