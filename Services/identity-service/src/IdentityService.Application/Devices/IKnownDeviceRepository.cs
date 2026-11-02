using IdentityService.Domain.Devices;

namespace IdentityService.Application.Devices;

public interface IKnownDeviceRepository
{
    Task<KnownDevice?> FindAsync(Guid userId, string fingerprintHash, CancellationToken cancellationToken);

    Task<KnownDevice?> FindByIdAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<KnownDevice>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(KnownDevice device, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
