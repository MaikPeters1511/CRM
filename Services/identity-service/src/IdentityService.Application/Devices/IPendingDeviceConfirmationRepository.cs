using IdentityService.Domain.Devices;

namespace IdentityService.Application.Devices;

public interface IPendingDeviceConfirmationRepository
{
    Task<PendingDeviceConfirmation?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(PendingDeviceConfirmation confirmation, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
