# identity-service

Authentifizierung: schlanker, selbst gehosteter JWT-Issuer (kein externer IdP, kein
OIDC-Discovery). Fachliche Spezifikation:
[`UserStories/authentifizierung.openspec.md`](../../UserStories/authentifizierung.openspec.md).

## Starten

```bash
cd Services/identity-service/src/IdentityService.Web
dotnet run --launch-profile http
```

Port (Dev, Direct): `http://localhost:5179`. Swagger/OpenAPI (nur `Development`):
`http://localhost:5179/swagger`. Üblicherweise wird der Service stattdessen über den
[AppHost](../../AppHost/README.md) mitgestartet (dort werden auch die Seed-Passwörter per
User Secrets gesetzt, siehe `Tests/E2E/README.md`, Abschnitt "Seed-Testnutzer").

## Endpoints

- `POST /api/auth/login` — `{ email, password }` → `{ accessToken, expiresAtUtc, userId, role, displayName }`
  (`[AllowAnonymous]`, Rate-Limited auf 5 Versuche/Minute/IP, `429` bei Überschreitung)

## Datenbank

Keine (`identity_db` bewusst nicht eingeführt, siehe OpenSpec Abschnitt 7). Zwei Seed-Testnutzer
(Admin/Employee) in-memory, Passwörter via `PasswordHasher<T>` gehasht, Seed-Werte aus
Konfiguration/Environment-Variablen.

## Abhängigkeiten

- Einziger Aussteller des JWT (Issuer/Audience/symmetrischer Signing-Key), das Gateway sowie
  `customer-service`/`employee-service`/`appointment-service` validieren dasselbe Token
  eigenständig erneut (Defense in Depth, siehe ADR 0001).
- Keine ausgehenden Service-zu-Service-Calls.
