namespace IdentityService.Infrastructure.Auth;

/// <summary>
/// Muss inhaltlich konsistent zur Authentication:JwtBearer-Konfiguration in Gateway,
/// customer-service und employee-service sein (gleicher Issuer/Audience/SigningKey),
/// sonst schlaegt die Token-Validierung in den Downstream-Services fehl.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Authentication:JwtBearer";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Symmetrischer Signing-Key. Wird NIE in appsettings.json/appsettings.Development.json
    /// hinterlegt, sondern ausschliesslich per Environment-Variable
    /// (Authentication__JwtBearer__SigningKey) bzw. .NET User Secrets injiziert
    /// (siehe AppHost.cs, Aspire-Parameter "jwt-signing-key").
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
