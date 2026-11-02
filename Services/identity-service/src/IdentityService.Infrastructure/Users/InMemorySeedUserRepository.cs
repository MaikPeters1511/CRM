using System.Collections.Concurrent;
using System.Security.Cryptography;
using IdentityService.Application.Auth;
using IdentityService.Domain.Users;
using Microsoft.AspNetCore.Identity; // Microsoft.Extensions.Identity.Core package
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Users;

/// <summary>
/// In-Memory-Nutzerspeicher, beim Start einmalig mit einem Admin- und einem Employee-Testnutzer
/// geseedet. Bewusste Vereinfachung fuer diesen Entwicklungsstand (kein identity_db/EF Core-Store):
/// Passwoerter werden NIE im Klartext im Code gehalten, sondern per PasswordHasher&lt;T&gt; gehasht;
/// die Klartext-Seed-Werte selbst kommen aus Konfiguration/Environment-Variablen und werden nach
/// dem Hashen sofort verworfen (nicht im Speicher behalten).
/// </summary>
public sealed class InMemorySeedUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _usersByEmail =
        new(StringComparer.OrdinalIgnoreCase);

    public InMemorySeedUserRepository(
        IOptions<IdentitySeedOptions> seedOptions,
        IHostEnvironment environment,
        ILogger<InMemorySeedUserRepository> logger)
    {
        var seed = seedOptions.Value;
        var hasher = new PasswordHasher<User>();

        SeedUser(seed.AdminEmail, seed.AdminPassword, seed.AdminDisplayName, UserRole.Admin, hasher, environment, logger);
        SeedUser(seed.EmployeeEmail, seed.EmployeePassword, seed.EmployeeDisplayName, UserRole.Employee, hasher, environment, logger);

        // Rolle Admin ist noetig, weil die Live-Endpoints (z.B. GET /api/appointments) fuer Employee
        // auf eigene Daten gefiltert sind; der Account wird ausschliesslich lesend vom ai-service
        // fuer den Qdrant-Index-Sync genutzt und existiert nur bei explizit konfiguriertem Passwort.
        if (!string.IsNullOrWhiteSpace(seed.ServiceAccountPassword))
        {
            SeedUser(seed.ServiceAccountEmail, seed.ServiceAccountPassword, seed.ServiceAccountDisplayName,
                UserRole.Admin, hasher, environment, logger);
        }
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        _usersByEmail.TryGetValue(email, out var user);
        return Task.FromResult(user);
    }

    private void SeedUser(
        string email,
        string? configuredPassword,
        string displayName,
        UserRole role,
        PasswordHasher<User> hasher,
        IHostEnvironment environment,
        ILogger logger)
    {
        var id = DeterministicIdFor(email);
        var password = configuredPassword;

        if (string.IsNullOrWhiteSpace(password))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"IdentitySeed-Passwort fuer '{email}' ist nicht konfiguriert. " +
                    "In Nicht-Development-Umgebungen muss es per Environment-Variable/Secret Manager gesetzt werden.");
            }

            password = GenerateRandomDevPassword();
            logger.LogWarning(
                "Kein IdentitySeed-Passwort fuer {Email} konfiguriert - generiere zufaelliges Dev-Passwort: {Password} " +
                "(nur fuer lokale Entwicklung; niemals in appsettings hardcoden).",
                email, password);
        }

        // Placeholder-User nur, um PasswordHasher<User> ein Objekt zu geben (Hash haengt nicht vom Inhalt ab).
        var placeholder = new User(id, email, string.Empty, role, displayName);
        var passwordHash = hasher.HashPassword(placeholder, password);

        _usersByEmail[email] = new User(id, email, passwordHash, role, displayName);
    }

    private static Guid DeterministicIdFor(string email)
    {
        var bytes = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return new Guid(bytes);
    }

    private static string GenerateRandomDevPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
}
