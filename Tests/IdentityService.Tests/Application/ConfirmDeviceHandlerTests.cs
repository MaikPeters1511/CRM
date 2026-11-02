using IdentityService.Application.Audit;
using IdentityService.Application.Devices;
using IdentityService.Domain.Devices;
using NSubstitute;

namespace IdentityService.Tests.Application;

/// <summary>Geraete-Bestaetigung per 6-stelligem Code (identity-device-trust.openspec.md).</summary>
public class ConfirmDeviceHandlerTests
{
    private static PendingDeviceConfirmation NewPending(string code, DateTimeOffset expiresAtUtc) => new(
        Guid.NewGuid(), Guid.NewGuid(), "fingerprint-hash", "test-agent", "203.0.113.0/24",
        ConfirmDeviceHandler.HashCode(code), expiresAtUtc);

    [Test]
    public async Task HandleAsync_WithCorrectCode_ConfirmsAndCreatesKnownDevice()
    {
        var pending = NewPending("654321", DateTimeOffset.UtcNow.AddMinutes(10));
        var pendingRepository = Substitute.For<IPendingDeviceConfirmationRepository>();
        pendingRepository.FindByIdAsync(pending.Id, Arg.Any<CancellationToken>()).Returns(pending);

        var knownDeviceRepository = Substitute.For<IKnownDeviceRepository>();
        var handler = new ConfirmDeviceHandler(
            pendingRepository, knownDeviceRepository, Substitute.For<IAuditLogger>(), TimeProvider.System);

        var result = await handler.HandleAsync(
            new ConfirmDeviceCommand(pending.Id, "654321", "203.0.113.1"), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(ConfirmDeviceStatus.Confirmed);
        await Assert.That(result.UserId).IsEqualTo(pending.UserId);
        await Assert.That(pending.IsConfirmed).IsTrue();
        await knownDeviceRepository.Received(1).AddAsync(
            Arg.Is<KnownDevice>(d => d.UserId == pending.UserId && d.FingerprintHash == pending.FingerprintHash),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_WithWrongCode_ReturnsInvalidCode()
    {
        var pending = NewPending("654321", DateTimeOffset.UtcNow.AddMinutes(10));
        var pendingRepository = Substitute.For<IPendingDeviceConfirmationRepository>();
        pendingRepository.FindByIdAsync(pending.Id, Arg.Any<CancellationToken>()).Returns(pending);

        var handler = new ConfirmDeviceHandler(
            pendingRepository, Substitute.For<IKnownDeviceRepository>(), Substitute.For<IAuditLogger>(), TimeProvider.System);

        var result = await handler.HandleAsync(
            new ConfirmDeviceCommand(pending.Id, "000000", "203.0.113.1"), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(ConfirmDeviceStatus.InvalidCode);
        await Assert.That(pending.IsConfirmed).IsFalse();
    }

    [Test]
    public async Task HandleAsync_WithExpiredConfirmation_ReturnsExpired()
    {
        var pending = NewPending("654321", DateTimeOffset.UtcNow.AddMinutes(-1));
        var pendingRepository = Substitute.For<IPendingDeviceConfirmationRepository>();
        pendingRepository.FindByIdAsync(pending.Id, Arg.Any<CancellationToken>()).Returns(pending);

        var handler = new ConfirmDeviceHandler(
            pendingRepository, Substitute.For<IKnownDeviceRepository>(), Substitute.For<IAuditLogger>(), TimeProvider.System);

        var result = await handler.HandleAsync(
            new ConfirmDeviceCommand(pending.Id, "654321", "203.0.113.1"), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(ConfirmDeviceStatus.Expired);
    }
}
