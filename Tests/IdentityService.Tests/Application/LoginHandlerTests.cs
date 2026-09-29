using IdentityService.Application.Auth;
using IdentityService.Application.Auth.Login;
using IdentityService.Domain.Users;
using NSubstitute;

namespace IdentityService.Tests.Application;

public class LoginHandlerTests
{
    private static readonly User AdminUser = new(
        Guid.NewGuid(), "admin@crm.local", "hashed-password", UserRole.Admin, "CRM Admin");

    [Test]
    public async Task HandleAsync_WithValidCredentials_ReturnsSuccessWithToken()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.FindByEmailAsync(AdminUser.Email, Arg.Any<CancellationToken>())
            .Returns(AdminUser);

        var passwordVerifier = Substitute.For<IPasswordVerifier>();
        passwordVerifier.Verify(AdminUser, "correct-password").Returns(true);

        var tokenGenerator = Substitute.For<ITokenGenerator>();
        var expiry = DateTimeOffset.UtcNow.AddHours(1);
        tokenGenerator.GenerateToken(AdminUser, out Arg.Any<DateTimeOffset>())
            .Returns(callInfo =>
            {
                callInfo[1] = expiry;
                return "fake-jwt-token";
            });

        var handler = new LoginHandler(repository, passwordVerifier, tokenGenerator);

        var result = await handler.HandleAsync(new LoginCommand(AdminUser.Email, "correct-password"), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(LoginStatus.Success);
        await Assert.That(result.AccessToken).IsEqualTo("fake-jwt-token");
        await Assert.That(result.Role).IsEqualTo(nameof(UserRole.Admin));
        await Assert.That(result.UserId).IsEqualTo(AdminUser.Id.ToString());
    }

    [Test]
    public async Task HandleAsync_WithWrongPassword_ReturnsInvalidCredentialsWithoutToken()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.FindByEmailAsync(AdminUser.Email, Arg.Any<CancellationToken>())
            .Returns(AdminUser);

        var passwordVerifier = Substitute.For<IPasswordVerifier>();
        passwordVerifier.Verify(AdminUser, "wrong-password").Returns(false);

        var tokenGenerator = Substitute.For<ITokenGenerator>();
        var handler = new LoginHandler(repository, passwordVerifier, tokenGenerator);

        var result = await handler.HandleAsync(new LoginCommand(AdminUser.Email, "wrong-password"), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(LoginStatus.InvalidCredentials);
        await Assert.That(result.AccessToken).IsNull();
        tokenGenerator.DidNotReceive().GenerateToken(Arg.Any<User>(), out Arg.Any<DateTimeOffset>());
    }

    [Test]
    public async Task HandleAsync_WithUnknownEmail_ReturnsInvalidCredentials()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.FindByEmailAsync("unknown@crm.local", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var passwordVerifier = Substitute.For<IPasswordVerifier>();
        var tokenGenerator = Substitute.For<ITokenGenerator>();
        var handler = new LoginHandler(repository, passwordVerifier, tokenGenerator);

        var result = await handler.HandleAsync(new LoginCommand("unknown@crm.local", "any-password"), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(LoginStatus.InvalidCredentials);
        passwordVerifier.DidNotReceive().Verify(Arg.Any<User>(), Arg.Any<string>());
    }
}
