namespace IdentityService.Domain.Users;

/// <summary>
/// Rollen gemaess RBAC-Modell aus kundenverwaltung.openspec.md / mitarbeiterverwaltung.openspec.md
/// Abschnitt 5. Der Wert wird 1:1 als "role"-Claim (ClaimTypes.Role) in das JWT uebernommen.
/// </summary>
public enum UserRole
{
    Employee,
    Admin,
}
