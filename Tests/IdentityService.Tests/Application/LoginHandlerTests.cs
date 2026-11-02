using IdentityService.Application.Audit;
using IdentityService.Application.Auth;
using IdentityService.Application.Auth.Login;
using IdentityService.Application.Devices;
using IdentityService.Application.Mfa;
using IdentityService.Application.Security;
using IdentityService.Domain.Devices;
using IdentityService.Domain.Mfa;
using IdentityService.Domain.Security;
using IdentityService.Domain.Users;
using NSubstitute;

namespace IdentityService.Tests.Application;

/// <summary>
/// Testet die Login-Gate-Reihenfolge Lockout -> Passwort -> Device-Trust -> MFA
/// (docs/adr/0004, Abschnitt 5). Jede Abhaengigkeit ist per Default "freundlich" konfiguriert
/// (bekanntes Geraet, kein MFA) - einzelne Tests ueberschreiben gezielt eine Abhaengigkeit, um das
/// jeweilige Gate isoliert zu pruefen.
/// </summary>
public class LoginHandlerTests
{
    private const string UserAgent = "test-agent";
    private const string IpAddress = "203.0.113.10";

    private static User NewUser() => new(Guid.NewGuid(), "admin@crm.local", "hashed-password", UserRole.Admin, "CRM Admin");

    private sealed class Fixture
    {
        public readonly IUserRepository UserRepository = Substitute.For<IUserRepository>();
        public readonly IPasswordVerifier PasswordVerifier = Substitute.For<IPasswordVerifier>();
        public readonly ITokenGenerator TokenGenerator = Substitute.For<ITokenGenerator>();
        public readonly ISecurityPolicyRepository SecurityPolicyRepository = Substitute.For<ISecurityPolicyRepository>();
        public readonly IKnownDeviceRepository KnownDeviceRepository = Substitute.For<IKnownDeviceRepository>();
        public readonly IPendingDeviceConfirmationRepository PendingDeviceConfirmationRepository = Substitute.For<IPendingDeviceConfirmationRepository>();
        public readonly IDeviceConfirmationNotifier DeviceConfirmationNotifier = Substitute.For<IDeviceConfirmationNotifier>();
        public readonly IMfaCredentialRepository MfaCredentialRepository = Substitute.For<IMfaCredentialRepository>();
        public readonly ILoginChallengeCache LoginChallengeCache = Substitute.For<ILoginChallengeCache>();
        public readonly IAuditLogger AuditLogger = Substitute.For<IAuditLogger>();

        public Fixture()
        {
            SecurityPolicyRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(new SecurityPolicy());
        }

        public void KnownDeviceFor(User user) =>
            KnownDeviceRepository
                .FindAsync(user.Id, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new KnownDevice(Guid.NewGuid(), user.Id, "hash", UserAgent, "203.0.113.0/24"));

        public LoginHandler Build() => new(
            UserRepository, PasswordVerifier, TokenGenerator, SecurityPolicyRepository,
            KnownDeviceRepository, PendingDeviceConfirmationRepository, DeviceConfirmationNotifier,
            MfaCredentialRepository, LoginChallengeCache, AuditLogger, TimeProvider.System);
    }

    [Test]
    public async Task HandleAsync_WithValidCredentialsAndKnownDeviceNoMfa_ReturnsSuccessWithToken()
    {
        var user = NewUser();
        var fixture = new Fixture();
        fixture.UserRepository.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        fixture.PasswordVerifier.Verify(user, "correct-password").Returns(true);
        fixture.KnownDeviceFor(user);

        var expiry = DateTimeOffset.UtcNow.AddHours(1);
        fixture.TokenGenerator.GenerateToken(user, out Arg.Any<DateTimeOffset>())
            .Returns(callInfo =>
            {
                callInfo[1] = expiry;
                return "fake-jwt-token";
            });

        var result = await fixture.Build().HandleAsync(
            new LoginCommand(user.Email, "correct-password", UserAgent, IpAddress), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(LoginStatus.Success);
        await Assert.That(result.AccessToken).IsEqualTo("fake-jwt-token");
        await Assert.That(result.Role).IsEqualTo(nameof(UserRole.Admin));
        await Assert.That(result.UserId).IsEqualTo(user.Id.ToString());
    }

    [Test]
    public async Task HandleAsync_WithWrongPassword_ReturnsInvalidCredentialsWithoutToken()
    {
        var user = NewUser();
        var fixture = new Fixture();
        fixture.UserRepository.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        fixture.PasswordVerifier.Verify(user, "wrong-password").Returns(false);

        var result = await fixture.Build().HandleAsync(
            new LoginCommand(user.Email, "wrong-password", UserAgent, IpAddress), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(LoginStatus.InvalidCredentials);
        await Assert.That(result.AccessToken).IsNull();
        fixture.TokenGenerator.DidNotReceive().GenerateToken(Arg.Any<User>(), out Arg.Any<DateTimeOffset>());
    }

    [Test]
    public async Task HandleAsync_WithUnknownEmail_ReturnsInvalidCredentials()
    {
        var fixture = new Fixture();
        fixture.UserRepository.FindByEmailAsync("unknown@crm.local", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await fixture.Build().HandleAsync(
            new LoginCommand("unknown@crm.local", "any-password", UserAgent, IpAddress), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(LoginStatus.InvalidCredentials);
        fixture.PasswordVerifier.DidNotReceive().Verify(Arg.Any<User>(), Arg.Any<string>());
    }

    [Test]
    public async Task HandleAsync_WhenAlreadyLockedOut_ReturnsLockedWithoutCheckingPassword()
    {
        var user = NewUser();
        var fixture = new Fixture();
        user.RecordFailedLoginAttempt(1, TimeSpan.FromMinutes(15), DateTimeOffset.UtcNow);
        fixture.UserRepository.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);

        var result = await fixture.Build().HandleAsync(
            new LoginCommand(user.Email, "irrelevant", UserAgent, IpAddress), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(LoginStatus.Locked);
        fixture.PasswordVerifier.DidNotReceive().Verify(Arg.Any<User>(), Arg.Any<string>());
    }

    [Test]
    public async Task HandleAsync_WithUnknownDevice_ReturnsDeviceConfirmationRequiredAndSendsCode()
    {
        var user = NewUser();
        var fixture = new Fixture();
        fixture.UserRepository.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        fixture.PasswordVerifier.Verify(user, "correct-password").Returns(true);
        // Kein KnownDeviceFor(user) - Repository liefert Default (null) => unbekanntes Geraet.

        var result = await fixture.Build().HandleAsync(
            new LoginCommand(user.Email, "correct-password", UserAgent, IpAddress), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(LoginStatus.DeviceConfirmationRequired);
        await Assert.That(result.PendingDeviceConfirmationId).IsNotNull();
        await Assert.That(result.AccessToken).IsNull();
        await fixture.DeviceConfirmationNotifier.Received(1).NotifyAsync(user.Email, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_WithMfaEnabledAndKnownDevice_ReturnsMfaRequiredChallengeToken()
    {
        var user = NewUser();
        user.EnableMfa();
        var fixture = new Fixture();
        fixture.UserRepository.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        fixture.PasswordVerifier.Verify(user, "correct-password").Returns(true);
        fixture.KnownDeviceFor(user);
        fixture.MfaCredentialRepository.FindByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(VerifiedCredentialFor(user));
        fixture.LoginChallengeCache.IssueToken(user.Id).Returns("challenge-token");

        var result = await fixture.Build().HandleAsync(
            new LoginCommand(user.Email, "correct-password", UserAgent, IpAddress), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(LoginStatus.MfaRequired);
        await Assert.That(result.MfaChallengeToken).IsEqualTo("challenge-token");
        await Assert.That(result.AccessToken).IsNull();
    }

    private static MfaCredential VerifiedCredentialFor(User user)
    {
        var credential = new MfaCredential(Guid.NewGuid(), user.Id, "SECRETSECRETSECRET");
        credential.MarkVerified();
        return credential;
    }
}
