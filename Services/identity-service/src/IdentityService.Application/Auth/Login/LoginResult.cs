namespace IdentityService.Application.Auth.Login;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
}

public sealed record LoginResult(
    LoginStatus Status,
    string? AccessToken = null,
    DateTimeOffset? ExpiresAtUtc = null,
    string? UserId = null,
    string? Role = null,
    string? DisplayName = null);
