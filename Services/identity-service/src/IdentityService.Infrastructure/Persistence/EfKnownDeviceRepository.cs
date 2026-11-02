using IdentityService.Application.Devices;
using IdentityService.Domain.Devices;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class EfKnownDeviceRepository(IdentityDbContext dbContext) : IKnownDeviceRepository
{
    public Task<KnownDevice?> FindAsync(Guid userId, string fingerprintHash, CancellationToken cancellationToken) =>
        dbContext.KnownDevices.FirstOrDefaultAsync(d => d.UserId == userId && d.FingerprintHash == fingerprintHash, cancellationToken);

    public Task<KnownDevice?> FindByIdAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken) =>
        dbContext.KnownDevices.FirstOrDefaultAsync(d => d.UserId == userId && d.Id == deviceId, cancellationToken);

    public async Task<IReadOnlyList<KnownDevice>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.KnownDevices.AsNoTracking().Where(d => d.UserId == userId).ToListAsync(cancellationToken);

    public async Task AddAsync(KnownDevice device, CancellationToken cancellationToken) =>
        await dbContext.KnownDevices.AddAsync(device, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
