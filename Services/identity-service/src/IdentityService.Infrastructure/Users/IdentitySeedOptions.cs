namespace IdentityService.Infrastructure.Users;

/// <summary>
/// Seed-Konfiguration fuer die zwei Test-Nutzer (Admin/Employee). Werte kommen aus
/// appsettings/appsettings.Development.json UND werden ueblicherweise per Environment-Variable
/// oder .NET User Secrets ueberschrieben (siehe AppHost.cs, Aspire-Parameter "identity-admin-password"
/// / "identity-employee-password") - niemals Klartext-Passwoerter im Quellcode.
/// Ist kein Passwort konfiguriert, generiert der Infrastructure-Layer beim Start ein zufaelliges
/// Passwort und loggt es einmalig (nur Development), statt ein Default-Passwort hart zu kodieren.
/// </summary>
public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public string AdminEmail { get; set; } = "admin@crm.local";

    public string? AdminPassword { get; set; }

    public string AdminDisplayName { get; set; } = "CRM Admin";

    public string EmployeeEmail { get; set; } = "employee@crm.local";

    public string? EmployeePassword { get; set; }

    public string EmployeeDisplayName { get; set; } = "CRM Employee";
}
