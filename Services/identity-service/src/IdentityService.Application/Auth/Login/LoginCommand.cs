namespace IdentityService.Application.Auth.Login;

public sealed record LoginCommand(string Email, string Password, string UserAgent, string IpAddress);
