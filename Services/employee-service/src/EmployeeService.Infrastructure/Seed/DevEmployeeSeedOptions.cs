namespace EmployeeService.Infrastructure.Seed;

/// <summary>
/// Konfiguration fuer den dev-/test-only Employee-Seeder (siehe <see cref="DevEmployeeSeedHostedService"/>).
///
/// Hintergrund (Tests/E2E/README.md, Bug 2): identity-service seedet einen Login-Testnutzer
/// "employee@crm.local" mit einer DETERMINISTISCHEN Id (MD5-Hash der lowercased E-Mail, siehe
/// IdentityService.Infrastructure.Users.InMemorySeedUserRepository.DeterministicIdFor). Ohne einen
/// passenden Employee-Datensatz mit EXAKT dieser Id existiert fuer diesen Nutzer nie ein "eigener"
/// Termin, wodurch der positive RBAC-Ownership-Pfad (JWT NameIdentifier == EmployeeId) nie end-to-end
/// erzeugbar ist.
///
/// <see cref="EmployeeUserId"/> ist deshalb bewusst auf denselben Wert vorbelegt, den
/// DeterministicIdFor("employee@crm.local") liefert. Es gibt (bewusst, kein Gold-Plating) KEINE
/// Cross-Service-Config-Referenz/kein gemeinsames Assembly - beide Services muessen bei einer
/// Aenderung von IdentitySeed:EmployeeEmail bzw. dieser Id manuell synchron gehalten werden
/// (Email-Default "employee@crm.local" ist bereits heute in beiden appsettings identisch).
/// Ueberschreibbar wie die anderen Seed-Werte per Environment-Variable/appsettings (kein Secret,
/// nur eine Id).
/// </summary>
public sealed class DevEmployeeSeedOptions
{
    public const string SectionName = "EmployeeSeed";

    public Guid EmployeeUserId { get; set; } = new("e573eed0-8c40-749f-f05c-aa6839c81cb4");

    public string Email { get; set; } = "employee@crm.local";

    public string FirstName { get; set; } = "CRM";

    public string LastName { get; set; } = "Employee";
}
