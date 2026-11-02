using IdentityService.Application.Devices;
using IdentityService.Domain.Devices;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public sealed class EfKnownDeviceRepository(IdentityDbContext dbContext) : IKnownDeviceRepository
{
    public async Task<KnownDevice?> FindAsync(Guid userId, string fingerprintHash, CancellationToken cancellationToken)
    {
        var existing = await dbContext.KnownDevices.FirstOrDefaultAsync(
            d => d.UserId == userId && d.FingerprintHash == fingerprintHash, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        // Solange kein SMTP-Notifier fuer den Bestaetigungscode und kein Device-Confirmation-Dialog im
        // Frontend angebunden sind (docs/adr/0004, Abschnitt 1 & 4), werden neue Geraete beim ersten
        // Login automatisch als bekannt registriert (Trust-On-First-Use). Durch einen Admin gesperrte
        // Geraete (IsRevoked = true) existieren bereits in KnownDevices und werden weiterhin vom
        // LoginHandler mit DeviceConfirmationRequired blockiert.
        var device = new KnownDevice(Guid.NewGuid(), userId, fingerprintHash, "auto", "0.0.0.0/0");
        await dbContext.KnownDevices.AddAsync(device, cancellationToken);
        return device;
    }

    public Task<KnownDevice?> FindByIdAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken) =>
        dbContext.KnownDevices.FirstOrDefaultAsync(d => d.UserId == userId && d.Id == deviceId, cancellationToken);

    public async Task<IReadOnlyList<KnownDevice>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.KnownDevices.AsNoTracking().Where(d => d.UserId == userId).ToListAsync(cancellationToken);

    public async Task AddAsync(KnownDevice device, CancellationToken cancellationToken) =>
        await dbContext.KnownDevices.AddAsync(device, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
