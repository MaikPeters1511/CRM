using IdentityService.Application.Audit;
using IdentityService.Domain.Audit;
using IdentityService.Domain.Devices;

namespace IdentityService.Application.Devices;

public sealed record ListUserDevicesQuery(Guid UserId);

public sealed class ListUserDevicesHandler(IKnownDeviceRepository repository)
{
    public Task<IReadOnlyList<KnownDevice>> HandleAsync(ListUserDevicesQuery query, CancellationToken cancellationToken) =>
        repository.ListByUserAsync(query.UserId, cancellationToken);
}

public sealed record RevokeDeviceCommand(Guid ActorUserId, Guid TargetUserId, Guid DeviceId, string? IpAddress);

public enum RevokeDeviceStatus
{
    Revoked,
    NotFound,
}

/// <summary>Admin sperrt ein Geraet eines Mitarbeiters (identity-device-trust.openspec.md).</summary>
public sealed class RevokeDeviceHandler(IKnownDeviceRepository repository, IAuditLogger auditLogger)
{
    public async Task<RevokeDeviceStatus> HandleAsync(RevokeDeviceCommand command, CancellationToken cancellationToken)
    {
        var device = await repository.FindByIdAsync(command.TargetUserId, command.DeviceId, cancellationToken);
        if (device is null)
        {
            return RevokeDeviceStatus.NotFound;
        }

        device.Revoke();
        await repository.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditEventType.DeviceRevoked, command.ActorUserId, command.TargetUserId,
            $"Geraet {command.DeviceId} gesperrt.", command.IpAddress, cancellationToken);

        return RevokeDeviceStatus.Revoked;
    }
}
