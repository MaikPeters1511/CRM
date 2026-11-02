using IdentityService.Application.Audit;
using IdentityService.Application.Auth;
using IdentityService.Application.Auth.Login;
using IdentityService.Application.Devices;
using IdentityService.Application.Mfa;
using IdentityService.Application.Passkeys;
using IdentityService.Application.Security;
using IdentityService.Infrastructure.Audit;
using IdentityService.Infrastructure.Auth;
using IdentityService.Infrastructure.Devices;
using IdentityService.Infrastructure.Mfa;
using IdentityService.Infrastructure.Passkeys;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<IdentitySeedOptions>(configuration.GetSection(IdentitySeedOptions.SectionName));
        services.Configure<Fido2Options>(configuration.GetSection(Fido2Options.SectionName));
        services.AddSingleton(new IssuerOptions(configuration["IdentitySeed:Issuer"] ?? "CRM"));

        // docs/adr/0004, Abschnitt 1: identity_db (PostgreSQL, EF Core) als gemeinsame Grundlage
        // fuer MFA/Passkeys/Device-Trust/Audit-Log - abloest die bisherige reine In-Memory-User-Haltung.
        var connectionString = configuration.GetConnectionString("identity_db")
            ?? Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "No connection string configured for 'identity_db'. Set ConnectionStrings:identity_db or IDENTITY_DB_CONNECTION_STRING.");

        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
        services.AddMemoryCache();

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IMfaCredentialRepository, EfMfaCredentialRepository>();
        services.AddScoped<IPasskeyCredentialRepository, EfPasskeyCredentialRepository>();
        services.AddScoped<IKnownDeviceRepository, EfKnownDeviceRepository>();
        services.AddScoped<IPendingDeviceConfirmationRepository, EfPendingDeviceConfirmationRepository>();
        services.AddScoped<ISecurityPolicyRepository, EfSecurityPolicyRepository>();
        services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
        services.AddScoped<IAuditLogger, EfAuditLogger>();

        services.AddSingleton<IPasswordVerifier, IdentityPasswordVerifier>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<ITotpProvider, OtpNetTotpProvider>();
        services.AddScoped<IFido2CeremonyService, Fido2CeremonyService>();
        services.AddSingleton<IDeviceConfirmationNotifier, LogDeviceConfirmationNotifier>();
        services.AddSingleton<ILoginChallengeCache, MemoryLoginChallengeCache>();
        services.AddSingleton(TimeProvider.System);

        // Tests/E2E/README.md, Bug 5-Muster: wendet beim Start automatisch alle ausstehenden
        // Migrationen an (jede Umgebung, kein separater Migrations-Deployment-Schritt).
        services.AddHostedService<DatabaseMigrationHostedService<IdentityDbContext>>();
        services.AddHostedService<IdentityDevUserSeedHostedService>();

        return services;
    }
}
