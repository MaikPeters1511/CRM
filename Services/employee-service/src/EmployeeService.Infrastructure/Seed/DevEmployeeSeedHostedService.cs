using EmployeeService.Application.Abstractions;
using EmployeeService.Domain.Employees;
using EmployeeService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmployeeService.Infrastructure.Seed;

/// <summary>
/// Dev-/Test-only Seeder: legt beim Start genau einen <see cref="Employee"/>-Datensatz mit der
/// bekannten Id des geseedeten identity-service-Testnutzers "employee@crm.local" an, sofern noch
/// keiner existiert. Damit besitzt dieser Login-Nutzer end-to-end einen echten Employee-Datensatz,
/// gegen den der Ownership-Check (JWT NameIdentifier == EmployeeId, siehe
/// terminverwaltung.openspec.md "Sichtbarkeit von Terminen nach Rolle") getestet werden kann
/// (siehe Tests/E2E/README.md, Bug 2).
///
/// NUR fuer die Development-Umgebung registriert (siehe
/// DependencyInjection.AddEmployeeServiceInfrastructure) - laeuft nie in Staging/Production.
/// Kein generisches Identity-Federation-Feature: ausschliesslich diese eine, fest bekannte
/// Verknuepfung fuer lokale Entwicklung/E2E-Tests.
/// </summary>
public sealed class DevEmployeeSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DevEmployeeSeedOptions> seedOptions,
    TimeProvider timeProvider,
    ILogger<DevEmployeeSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seed = seedOptions.Value;

        using var scope = scopeFactory.CreateScope();

        // Wendet die bereits vorhandene InitialCreate-Migration an, falls noch nicht geschehen.
        // Nur relevant fuer lokale Aspire-Dev-Umgebungen mit frischem (nicht-persistentem)
        // Postgres-Container; KEIN neuer Migrations-Mechanismus, nur ein Development-only
        // Bequemlichkeits-Schritt, damit dieser dev-only Seeder (und der Service selbst)
        // nicht an einem fehlenden Schema scheitern. Fehler hier duerfen den Service-Start
        // NIEMALS verhindern (z.B. in Tests mit Fake-Repository/keiner echten Datenbank).
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EmployeeDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Dev-Employee-Seed: EmployeeDbContext konnte nicht migriert werden (z.B. keine echte Datenbank " +
                "in Tests) - Service startet trotzdem weiter.");
        }

        // Das eigentliche Seeding darf ebenfalls niemals den Service-Start verhindern - dies ist
        // ausschliesslich eine dev-/test-only Komfortfunktion (siehe Klassen-Doku).
        try
        {
            var repository = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();

            var existing = await repository.GetByIdAsync(seed.EmployeeUserId, cancellationToken);
            if (existing is not null)
            {
                return;
            }

            var result = Employee.CreateWithId(
                seed.EmployeeUserId,
                seed.FirstName,
                seed.LastName,
                seed.Email,
                EmployeeRole.Employee,
                createdBy: "dev-seed",
                createdAt: timeProvider.GetUtcNow());

            if (!result.IsSuccess)
            {
                logger.LogError(
                    "Dev-Employee-Seed fehlgeschlagen (Id {EmployeeId}): {Errors}",
                    seed.EmployeeUserId, string.Join("; ", result.Errors));
                return;
            }

            await repository.AddAsync(result.Employee!, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Dev-Employee-Seed: Employee {EmployeeId} ({Email}) angelegt, damit der identity-service-Testnutzer " +
                "ueber den Ownership-Check (NameIdentifier == EmployeeId) end-to-end getestet werden kann.",
                seed.EmployeeUserId, seed.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dev-Employee-Seed fehlgeschlagen - Service startet trotzdem weiter (dev-only, nicht kritisch).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
