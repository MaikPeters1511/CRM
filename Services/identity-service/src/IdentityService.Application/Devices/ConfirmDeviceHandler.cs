using System.Security.Cryptography;
using System.Text;
using IdentityService.Application.Audit;
using IdentityService.Domain.Audit;
using IdentityService.Domain.Devices;

namespace IdentityService.Application.Devices;

public sealed record ConfirmDeviceCommand(Guid PendingConfirmationId, string Code, string? IpAddress);

public enum ConfirmDeviceStatus
{
    Confirmed,
    InvalidCode,
    Expired,
    NotFound,
}

public sealed record ConfirmDeviceResult(ConfirmDeviceStatus Status, Guid? UserId);

/// <summary>Schliesst die Geraete-Bestaetigung ab (identity-device-trust.openspec.md) und merkt das Geraet als bekannt vor.</summary>
public sealed class ConfirmDeviceHandler(
    IPendingDeviceConfirmationRepository pendingRepository,
    IKnownDeviceRepository knownDeviceRepository,
    IAuditLogger auditLogger,
    TimeProvider timeProvider)
{
    public async Task<ConfirmDeviceResult> HandleAsync(ConfirmDeviceCommand command, CancellationToken cancellationToken)
    {
        var pending = await pendingRepository.FindByIdAsync(command.PendingConfirmationId, cancellationToken);
        if (pending is null)
        {
            return new ConfirmDeviceResult(ConfirmDeviceStatus.NotFound, null);
        }

        var now = timeProvider.GetUtcNow();
        if (pending.IsExpired(now))
        {
            return new ConfirmDeviceResult(ConfirmDeviceStatus.Expired, null);
        }

        if (pending.CodeHash != HashCode(command.Code))
        {
            return new ConfirmDeviceResult(ConfirmDeviceStatus.InvalidCode, null);
        }

        pending.MarkConfirmed();

        var existing = await knownDeviceRepository.FindAsync(pending.UserId, pending.FingerprintHash, cancellationToken);
        if (existing is not null)
        {
            existing.Unrevoke();
            existing.RecordSeen(pending.UserAgent, pending.LastIpRange);
        }
        else
        {
            await knownDeviceRepository.AddAsync(
                new KnownDevice(Guid.NewGuid(), pending.UserId, pending.FingerprintHash, pending.UserAgent, pending.LastIpRange),
                cancellationToken);
        }

        await pendingRepository.SaveChangesAsync(cancellationToken);
        await knownDeviceRepository.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditEventType.DeviceApproved, pending.UserId, pending.UserId,
            $"Geraet bestaetigt (Fingerprint {pending.FingerprintHash[..8]}...).", command.IpAddress, cancellationToken);

        return new ConfirmDeviceResult(ConfirmDeviceStatus.Confirmed, pending.UserId);
    }

    public static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
