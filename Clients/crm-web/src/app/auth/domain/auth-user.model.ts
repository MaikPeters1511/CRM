/**
 * Domain-Modell des authentifizierten Nutzers, abgeleitet aus den Claims des JWT
 * (siehe identity-service, Auth/Login/LoginResult). Rollen gemaess RBAC-Modell aus
 * kundenverwaltung.openspec.md Abschnitt 5.
 */
export type UserRole = 'Employee' | 'Admin';

export interface AuthUser {
  userId: string;
  role: UserRole;
  displayName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  role: UserRole;
  displayName: string;
}
