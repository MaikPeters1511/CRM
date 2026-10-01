# Capability: Identity & Authentication

> **Status:** active
> Implementiert durch `identity-service` (schlanker, selbst gehosteter JWT-Issuer). Archivierte Change-Historie: `UserStories/archive/2026-10-01-authentifizierung.openspec.md`.

## Zweck
Authentifizierung (Login/JWT-Ausstellung) und die technische Grundlage für RBAC/Ownership-Checks in allen anderen Services.

## Requirements

#### Requirement: Login gegen identity-service
Das System SHALL einen `POST /api/auth/login`-Endpoint bereitstellen, der bei gültigen Anmeldedaten ein signiertes JWT mit den Claims `sub`/`ClaimTypes.NameIdentifier` (User-Id) und `ClaimTypes.Role` (Rolle) liefert.

##### Scenario: Erfolgreicher Login als Admin
- **GIVEN** ein geseedeter Admin-Nutzer mit bekannter E-Mail und Passwort
- **WHEN** `POST /api/auth/login` mit diesen Anmeldedaten aufgerufen wird
- **THEN** antwortet der Service mit 200 OK, einem JWT und dem Rollen-Claim `Admin`

##### Scenario: Fehlgeschlagener Login mit falschem Passwort
- **GIVEN** ein geseedeter Nutzer
- **WHEN** `POST /api/auth/login` mit falschem Passwort aufgerufen wird
- **THEN** antwortet der Service mit 401 Unauthorized, ohne zu verraten, ob E-Mail oder Passwort falsch war

##### Scenario: Fehlgeschlagener Login mit unbekannter E-Mail
- **GIVEN** eine nicht existierende E-Mail-Adresse
- **WHEN** `POST /api/auth/login` damit aufgerufen wird
- **THEN** antwortet der Service mit 401 Unauthorized (identisches Verhalten wie bei falschem Passwort, keine User-Enumeration)

#### Requirement: RBAC-Validierung serverseitig in jedem Service (Defense in Depth)
Jeder Service (Gateway, customer-service, employee-service, appointment-service) SHALL das JWT eigenständig validieren und darf sich nicht ausschließlich auf die Auth-Termination des Gateways verlassen.

##### Scenario: Zugriff ohne Token
- **GIVEN** kein Authorization-Header
- **WHEN** ein geschützter Endpoint aufgerufen wird
- **THEN** antwortet der Service mit 401 Unauthorized

##### Scenario: Zugriff mit falscher Rolle
- **GIVEN** ein gültiges Token mit Rolle `Employee`
- **WHEN** ein nur für `Admin` erlaubter Endpoint aufgerufen wird
- **THEN** antwortet der Service mit 403 Forbidden

#### Requirement: Ownership-Check bei Mitarbeiter-Abwesenheiten
Ein Mitarbeiter (Rolle `Employee`) SHALL nur Abwesenheiten für seinen eigenen Employee-Datensatz anlegen können; ein Admin darf für beliebige Mitarbeiter Abwesenheiten anlegen.

##### Scenario: Mitarbeiter legt eigene Abwesenheit an
- **GIVEN** ein Token mit `NameIdentifier` = Employee-Id X, Rolle `Employee`
- **WHEN** `POST /api/employees/{X}/absences` aufgerufen wird
- **THEN** wird die Abwesenheit angelegt (201 Created)

##### Scenario: Mitarbeiter versucht fremde Abwesenheit anzulegen
- **GIVEN** ein Token mit `NameIdentifier` = Employee-Id X, Rolle `Employee`
- **WHEN** `POST /api/employees/{Y}/absences` mit Y != X aufgerufen wird
- **THEN** antwortet der Service mit 403 Forbidden

#### Requirement: Session-Handling im Frontend
Das Frontend SHALL das JWT nach Login in `sessionStorage` speichern, per HTTP-Interceptor an alle API-Requests anhängen und bei 401-Antworten die Session löschen und zur Login-Seite umleiten.

##### Scenario: Navigation ohne gültige Session
- **GIVEN** kein gespeichertes Token (oder ein abgelaufenes Token)
- **WHEN** eine geschützte Route aufgerufen wird
- **THEN** leitet der Route Guard zur Login-Seite um (mit `returnUrl`)

##### Scenario: Automatischer Logout bei 401
- **GIVEN** eine gespeicherte Session
- **WHEN** ein API-Request mit 401 antwortet
- **THEN** wird die Session gelöscht und zur Login-Seite umgeleitet

#### Requirement: Brute-Force-Schutz auf dem Login-Endpoint
Der Login-Endpoint SHALL die Anzahl der Versuche pro Client-IP und Zeitfenster begrenzen (Rate Limiting).

##### Scenario: Zu viele Login-Versuche
- **GIVEN** mehr als 5 Login-Versuche derselben IP innerhalb einer Minute
- **WHEN** ein weiterer Login-Versuch erfolgt
- **THEN** antwortet der Service mit 429 Too Many Requests

## API
- `POST /api/auth/login`: `{ email, password }` → `{ accessToken, expiresAtUtc, userId, role, displayName }`, über Gateway `/api/auth/**` anonym erreichbar.

## Bewusste Vereinfachungen (Stand heute)
- Kein externer IdP/OIDC-Discovery; symmetrischer JWT-Signing-Key statt Authority-Metadaten.
- Kein `identity_db`; zwei geseedete Testnutzer (Admin/Employee), In-Memory pro Prozessstart, Passwörter gehasht via `PasswordHasher<T>`.
- Keine Refresh-Token-Rotation (Access Token 60 Minuten gültig).
- `sessionStorage` statt `localStorage` (begrenzt XSS-Zeitfenster), kein HttpOnly-Cookie-Flow.

## Architektur-Verweise
- `docs/adr/0001-microservices-architektur-crm.md` Abschnitt 4 (Defense in Depth, Gateway terminiert Auth, Services validieren erneut)
- `Services/identity-service/README.md`

## Tests
- `Tests/IdentityService.Tests`, `Tests/CrmWeb.Tests` (Auth-Flow), `Tests/E2E/tests/auth.spec.ts` (6/6 grün).
