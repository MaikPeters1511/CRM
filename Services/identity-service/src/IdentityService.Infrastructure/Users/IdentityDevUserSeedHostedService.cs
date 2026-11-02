using IdentityService.Domain.Users;
using IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Users;

/// <summary>
/// Seed der Testnutzer (Admin/Employee/optionaler Service-Account) direkt in identity_db
/// (docs/adr/0004, Abschnitt 1). Ersetzt die bisherige rein In-Memory-Seeding-Logik in
/// <c>InMemorySeedUserRepository</c>; identisches Verhalten (deterministische Ids,
/// PasswordHasher&lt;T&gt;, Zufalls-Dev-Passwort NUR in Development falls nicht konfiguriert, sonst
/// Exception), nur jetzt persistiert. Prueft bewusst JE E-Mail (nicht nur "Tabelle insgesamt leer"),
/// damit ein spaeter konfigurierter Service-Account (z.B. IdentitySeed:ServiceAccountPassword)
/// auch dann noch angelegt wird, wenn Admin/Employee bereits existieren (gilt sowohl fuer
/// Produktion als auch fuer die gemeinsam genutzte Testcontainer-DB in IdentityServiceWebFactory).
/// </summary>
public sealed class IdentityDevUserSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<IdentitySeedOptions> seedOptions,
    IHostEnvironment environment,
    ILogger<IdentityDevUserSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);

            var seed = seedOptions.Value;
            var hasher = new PasswordHasher<User>();

            await SeedUserIfMissingAsync(dbContext, seed.AdminEmail, seed.AdminPassword, seed.AdminDisplayName, UserRole.Admin, hasher, cancellationToken);
            await SeedUserIfMissingAsync(dbContext, seed.EmployeeEmail, seed.EmployeePassword, seed.EmployeeDisplayName, UserRole.Employee, hasher, cancellationToken);

            if (!string.IsNullOrWhiteSpace(seed.ServiceAccountPassword))
            {
                await SeedUserIfMissingAsync(dbContext, seed.ServiceAccountEmail, seed.ServiceAccountPassword, seed.ServiceAccountDisplayName, UserRole.Admin, hasher, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dev-User-Seed fuer identity_db fehlgeschlagen - Service startet trotzdem weiter.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedUserIfMissingAsync(
        IdentityDbContext dbContext, string email, string? configuredPassword, string displayName, UserRole role,
        PasswordHasher<User> hasher, CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return;
        }

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

        var placeholder = new User(id, email, string.Empty, role, displayName);
        var passwordHash = hasher.HashPassword(placeholder, password);

        dbContext.Users.Add(new User(id, email, passwordHash, role, displayName));
    }

    private static Guid DeterministicIdFor(string email)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return new Guid(bytes);
    }

    private static string GenerateRandomDevPassword() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(18));
}
