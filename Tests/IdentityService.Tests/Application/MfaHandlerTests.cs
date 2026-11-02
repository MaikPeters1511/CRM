using IdentityService.Application.Audit;
using IdentityService.Application.Auth;
using IdentityService.Application.Mfa;
using IdentityService.Domain.Audit;
using IdentityService.Domain.Mfa;
using IdentityService.Domain.Users;
using NSubstitute;

namespace IdentityService.Tests.Application;

/// <summary>TOTP-MFA-Einrichtung/Verifizierung/Deaktivierung (identity-mfa-totp.openspec.md).</summary>
public class MfaHandlerTests
{
    private static readonly User User = new(Guid.NewGuid(), "admin@crm.local", "hashed-password", UserRole.Admin, "CRM Admin");

    [Test]
    public async Task Setup_GeneratesSecretAndStoresUnverifiedCredential()
    {
        var repository = Substitute.For<IMfaCredentialRepository>();
        var totpProvider = Substitute.For<ITotpProvider>();
        totpProvider.GenerateSecret().Returns("GENERATEDSECRET");
        totpProvider.BuildOtpAuthUri("GENERATEDSECRET", User.Email, "CRM").Returns("otpauth://totp/test");

        var handler = new MfaSetupHandler(repository, totpProvider, new IssuerOptions("CRM"));

        var result = await handler.HandleAsync(new MfaSetupCommand(User.Id, User.Email), CancellationToken.None);

        await Assert.That(result.SecretBase32).IsEqualTo("GENERATEDSECRET");
        await Assert.That(result.OtpAuthUri).IsEqualTo("otpauth://totp/test");
        await repository.Received(1).AddAsync(
            Arg.Is<MfaCredential>(c => c.UserId == User.Id && !c.Verified), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Setup_WhenAlreadyVerified_Throws()
    {
        var repository = Substitute.For<IMfaCredentialRepository>();
        var existing = new MfaCredential(Guid.NewGuid(), User.Id, "SECRET");
        existing.MarkVerified();
        repository.FindByUserIdAsync(User.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var handler = new MfaSetupHandler(repository, Substitute.For<ITotpProvider>(), new IssuerOptions("CRM"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new MfaSetupCommand(User.Id, User.Email), CancellationToken.None));
    }

    [Test]
    public async Task Verify_WithValidCode_ActivatesMfaAndLogsAuditEvent()
    {
        var credentialRepository = Substitute.For<IMfaCredentialRepository>();
        var credential = new MfaCredential(Guid.NewGuid(), User.Id, "SECRET");
        credentialRepository.FindByUserIdAsync(User.Id, Arg.Any<CancellationToken>()).Returns(credential);

        var userRepository = Substitute.For<IUserRepository>();
        userRepository.FindByIdAsync(User.Id, Arg.Any<CancellationToken>()).Returns(User);

        var totpProvider = Substitute.For<ITotpProvider>();
        totpProvider.ValidateCode("SECRET", "123456").Returns(true);

        var auditLogger = Substitute.For<IAuditLogger>();

        var handler = new MfaVerifyHandler(credentialRepository, userRepository, totpProvider, auditLogger);

        var status = await handler.HandleAsync(new MfaVerifyCommand(User.Id, "123456", "203.0.113.1"), CancellationToken.None);

        await Assert.That(status).IsEqualTo(MfaVerifyStatus.Activated);
        await Assert.That(credential.Verified).IsTrue();
        await Assert.That(User.MfaEnabled).IsTrue();
        await auditLogger.Received(1).LogAsync(
            AuditEventType.MfaEnabled, User.Id, User.Id, Arg.Any<string>(), "203.0.113.1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Verify_WithInvalidCode_DoesNotActivateMfa()
    {
        var credentialRepository = Substitute.For<IMfaCredentialRepository>();
        var credential = new MfaCredential(Guid.NewGuid(), User.Id, "SECRET");
        credentialRepository.FindByUserIdAsync(User.Id, Arg.Any<CancellationToken>()).Returns(credential);

        var totpProvider = Substitute.For<ITotpProvider>();
        totpProvider.ValidateCode("SECRET", "000000").Returns(false);

        var handler = new MfaVerifyHandler(
            credentialRepository, Substitute.For<IUserRepository>(), totpProvider, Substitute.For<IAuditLogger>());

        var status = await handler.HandleAsync(new MfaVerifyCommand(User.Id, "000000", null), CancellationToken.None);

        await Assert.That(status).IsEqualTo(MfaVerifyStatus.InvalidCode);
        await Assert.That(credential.Verified).IsFalse();
    }

    [Test]
    public async Task Disable_WithWrongPassword_ReturnsInvalidPasswordAndKeepsMfaEnabled()
    {
        var user = new User(Guid.NewGuid(), "admin2@crm.local", "hash", UserRole.Admin, "Admin 2");
        user.EnableMfa();

        var userRepository = Substitute.For<IUserRepository>();
        userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var passwordVerifier = Substitute.For<IPasswordVerifier>();
        passwordVerifier.Verify(user, "wrong").Returns(false);

        var handler = new MfaDisableHandler(
            userRepository, Substitute.For<IMfaCredentialRepository>(), passwordVerifier, Substitute.For<IAuditLogger>());

        var status = await handler.HandleAsync(new MfaDisableCommand(user.Id, "wrong", null), CancellationToken.None);

        await Assert.That(status).IsEqualTo(MfaDisableStatus.InvalidPassword);
        await Assert.That(user.MfaEnabled).IsTrue();
    }
}
