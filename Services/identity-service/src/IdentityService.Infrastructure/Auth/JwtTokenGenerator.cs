using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Application.Auth;
using IdentityService.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Auth;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options) : ITokenGenerator
{
    public string GenerateToken(User user, out DateTimeOffset expiresAtUtc)
    {
        var jwtOptions = options.Value;

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException(
                "Authentication:JwtBearer:SigningKey ist nicht konfiguriert. " +
                "Signing-Key muss per Environment-Variable/.NET User Secrets gesetzt werden (kein Hardcoding).");
        }

        var now = DateTimeOffset.UtcNow;
        expiresAtUtc = now.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes);

        // Claims-Schema: "sub"/ClaimTypes.NameIdentifier tragen die User-Id, damit
        // employee-service den Ownership-Check bei Abwesenheiten (Mitarbeiter darf nur
        // eigene Abwesenheit anlegen) gegen den authentifizierten Nutzer pruefen kann.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
