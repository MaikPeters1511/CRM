using IdentityService.Application.Audit;
using IdentityService.Application.Auth;
using IdentityService.Application.Auth.ChangePassword;
using IdentityService.Application.Devices;
using IdentityService.Application.Security;
using IdentityService.Domain.Audit;
using IdentityService.Domain.Devices;
using IdentityService.Domain.Security;
using IdentityService.Domain.Users;
using NSubstitute;

namespace IdentityService.Tests.Application;

public class UpdateSecurityPolicyHandlerTests
{
    [Test]
    public async Task HandleAsync_UpdatesPolicyAndLogsAuditEvent()
    {
        var repository = Substitute.For<ISecurityPolicyRepository>();
        repository.GetAsync(Arg.Any<CancellationToken>()).Returns(new SecurityPolicy());
        var auditLogger = Substitute.For<IAuditLogger>();
        var actorId = Guid.NewGuid();

        var handler = new UpdateSecurityPolicyHandler(repository, auditLogger);

        var policy = await handler.HandleAsync(
            new UpdateSecurityPolicyCommand(actorId, true, 12, 60, 3, 30, "203.0.113.1"), CancellationToken.None);

        await Assert.That(policy.RequireMfa).IsTrue();
        await Assert.That(policy.PasswordMinLength).IsEqualTo(12);
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await auditLogger.Received(1).LogAsync(
            AuditEventType.SecurityPolicyChanged, actorId, null, Arg.Any<string>(), "203.0.113.1", Arg.Any<CancellationToken>());
    }
}

public class ChangePasswordHandlerTests
{
    private static User NewUser() => new(Guid.NewGuid(), "user@crm.local", "hash", UserRole.Employee, "Test User");

    [Test]
    public async Task HandleAsync_WithPasswordShorterThanPolicyMinimum_ReturnsPolicyViolation()
    {
        var user = NewUser();
        var userRepository = Substitute.For<IUserRepository>();
        userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var passwordVerifier = Substitute.For<IPasswordVerifier>();
        passwordVerifier.Verify(user, "current-pw").Returns(true);

        var policyRepository = Substitute.For<ISecurityPolicyRepository>();
        var policy = new SecurityPolicy();
        policy.Update(false, passwordMinLength: 12, 90, 5, 15);
        policyRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(policy);

        var handler = new ChangePasswordHandler(userRepository, passwordVerifier, policyRepository, Substitute.For<IAuditLogger>());

        var status = await handler.HandleAsync(
            new ChangePasswordCommand(user.Id, "current-pw", "short1", null), CancellationToken.None);

        await Assert.That(status).IsEqualTo(ChangePasswordStatus.PolicyViolation);
    }

    [Test]
    public async Task HandleAsync_WithValidNewPassword_UpdatesHashAndLogsAuditEvent()
    {
        var user = NewUser();
        var userRepository = Substitute.For<IUserRepository>();
        userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var passwordVerifier = Substitute.For<IPasswordVerifier>();
        passwordVerifier.Verify(user, "current-pw").Returns(true);
        passwordVerifier.Hash(user, "a-long-enough-password").Returns("new-hash");

        var policyRepository = Substitute.For<ISecurityPolicyRepository>();
        policyRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(new SecurityPolicy());

        var auditLogger = Substitute.For<IAuditLogger>();
        var handler = new ChangePasswordHandler(userRepository, passwordVerifier, policyRepository, auditLogger);

        var status = await handler.HandleAsync(
            new ChangePasswordCommand(user.Id, "current-pw", "a-long-enough-password", "203.0.113.1"), CancellationToken.None);

        await Assert.That(status).IsEqualTo(ChangePasswordStatus.Changed);
        await Assert.That(user.PasswordHash).IsEqualTo("new-hash");
        await auditLogger.Received(1).LogAsync(
            AuditEventType.PasswordChanged, user.Id, user.Id, Arg.Any<string>(), "203.0.113.1", Arg.Any<CancellationToken>());
    }
}

public class RevokeDeviceHandlerTests
{
    [Test]
    public async Task HandleAsync_RevokesExistingDevice()
    {
        var targetUserId = Guid.NewGuid();
        var device = new KnownDevice(Guid.NewGuid(), targetUserId, "hash", "agent", "203.0.113.0/24");

        var repository = Substitute.For<IKnownDeviceRepository>();
        repository.FindByIdAsync(targetUserId, device.Id, Arg.Any<CancellationToken>()).Returns(device);

        var handler = new RevokeDeviceHandler(repository, Substitute.For<IAuditLogger>());

        var status = await handler.HandleAsync(
            new RevokeDeviceCommand(Guid.NewGuid(), targetUserId, device.Id, null), CancellationToken.None);

        await Assert.That(status).IsEqualTo(RevokeDeviceStatus.Revoked);
        await Assert.That(device.IsRevoked).IsTrue();
    }

    [Test]
    public async Task HandleAsync_WithUnknownDevice_ReturnsNotFound()
    {
        var repository = Substitute.For<IKnownDeviceRepository>();
        var handler = new RevokeDeviceHandler(repository, Substitute.For<IAuditLogger>());

        var status = await handler.HandleAsync(
            new RevokeDeviceCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null), CancellationToken.None);

        await Assert.That(status).IsEqualTo(RevokeDeviceStatus.NotFound);
    }
}
