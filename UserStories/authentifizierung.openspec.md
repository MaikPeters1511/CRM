# Change: Authentifizierung & Identity

> **Status:** in-progress
> Ablage: `UserStories/authentifizierung.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-authentifizierung.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** `customer-service`, `employee-service` und das Gateway hatten bereits `[Authorize(Roles=...)]`
  bzw. eine JwtBearer-Konfiguration, aber `Authentication:JwtBearer:Authority` war ueberall leer/Platzhalter.
  Es existierte kein echter Login/Token-Ausstellungs-Flow und kein Identity Provider. RBAC/Ownership-Checks
  waren zwar im Code vorbereitet, konnten aber nie mit einem echten Token verifiziert werden. Ohne diese
  Capability ist "Sicherheit" in diesem System nicht mehr als unbestaetigte Annahmen.
- **What Changes:**
  - Neuer, eigenstaendiger `identity-service` (schlanker, selbst gehosteter JWT-Issuer, kein externer IdP,
    kein vollwertiges OAuth2/OIDC-Produkt) mit `POST /api/auth/login`.
  - Zwei Seed-Testnutzer (Admin/Employee), Passwoerter gehasht via `PasswordHasher<T>` (Microsoft.AspNetCore.Identity),
    Seed-Werte aus Konfiguration/Environment-Variablen (kein Klartext-Passwort im Code).
  - Einheitliches JWT-Schema (Issuer/Audience/symmetrischer Signing-Key) fuer identity-service (Aussteller) und
    Gateway/customer-service/employee-service (Validierer, Defense in Depth), verteilt ueber Aspire-Parameter
    (`jwt-issuer`, `jwt-audience`, `jwt-signing-key` als Secret) statt Hardcoding.
  - Gateway leitet `/api/auth/**` anonym an identity-service weiter, terminiert Auth fuer `/api/customers/**`
    und `/api/employees/**`, leitet das validierte Token unveraendert an die Downstream-Services weiter.
  - Frontend-Login-Flow (customer-web, employee-web): Login-Formular, Token in `sessionStorage`, HTTP-Interceptor,
    Route Guards.
  - OWASP-Basics: Rate-Limiting auf Login, restriktive CORS-Policy, HTTPS-Redirect.
  - **BREAKING**: `Authentication:JwtBearer:Authority`/`Audience`-Schema wurde durch `Issuer`/`Audience`/`SigningKey`
    ersetzt (kein OIDC-Discovery-Endpoint); bestehende (leere) Konfiguration war ohnehin nicht funktionsfaehig.
- **Impact:** Neuer Bounded Context `Identity` (identity-service). Aenderungen an Gateway, customer-service,
  employee-service (nur Authentication-Konfiguration, RBAC/Ownership-Logik war bereits korrekt und blieb
  unveraendert), AppHost, customer-web, employee-web.

## 2. User Stories
- Als Mitarbeiter/Admin moechte ich mich mit E-Mail/Passwort anmelden, damit ich ein gueltiges JWT fuer den
  Zugriff auf customer-service/employee-service erhalte.
- Als Admin moechte ich, dass nur Nutzer mit der Rolle `Admin` bestimmte Aktionen (z.B. Kunde/Mitarbeiter
  deaktivieren) ausfuehren koennen.
- Als Mitarbeiter moechte ich nur meine eigenen Abwesenheiten anlegen koennen (Ownership-Check), nicht die
  eines anderen Mitarbeiters.
- Als Nutzer moechte ich bei einer abgelaufenen/ungueltigen Session automatisch zur Login-Seite geleitet werden.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Login gegen identity-service
Das System SHALL einen `POST /api/auth/login`-Endpoint bereitstellen, der bei gueltigen Anmeldedaten ein
signiertes JWT mit den Claims `sub`/`ClaimTypes.NameIdentifier` (User-Id) und `ClaimTypes.Role` (Rolle) liefert.

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
- **THEN** antwortet der Service mit 401 Unauthorized (identisches Verhalten wie bei falschem Passwort,
  um User-Enumeration zu verhindern)

#### Requirement: RBAC-Validierung serverseitig in jedem Service (Defense in Depth)
Jeder Service (Gateway, customer-service, employee-service) SHALL das JWT eigenstaendig validieren
(Issuer/Audience/Signatur/Ablauf) und darf sich nicht ausschliesslich auf die Auth-Termination des
Gateways verlassen.

##### Scenario: Zugriff ohne Token
- **GIVEN** kein Authorization-Header
- **WHEN** ein geschuetzter Endpoint aufgerufen wird
- **THEN** antwortet der Service mit 401 Unauthorized

##### Scenario: Zugriff mit falscher Rolle
- **GIVEN** ein gueltiges Token mit Rolle `Employee`
- **WHEN** ein nur fuer `Admin` erlaubter Endpoint aufgerufen wird (z.B. Kunde/Mitarbeiter deaktivieren)
- **THEN** antwortet der Service mit 403 Forbidden

#### Requirement: Ownership-Check bei Mitarbeiter-Abwesenheiten
Ein Mitarbeiter (Rolle `Employee`) SHALL nur Abwesenheiten fuer seinen eigenen Employee-Datensatz anlegen
koennen; ein Admin darf fuer beliebige Mitarbeiter Abwesenheiten anlegen.

##### Scenario: Mitarbeiter legt eigene Abwesenheit an
- **GIVEN** ein Token mit `NameIdentifier` = Employee-Id X, Rolle `Employee`
- **WHEN** `POST /api/employees/{X}/absences` aufgerufen wird
- **THEN** wird die Abwesenheit angelegt (201 Created)

##### Scenario: Mitarbeiter versucht fremde Abwesenheit anzulegen
- **GIVEN** ein Token mit `NameIdentifier` = Employee-Id X, Rolle `Employee`
- **WHEN** `POST /api/employees/{Y}/absences` mit Y != X aufgerufen wird
- **THEN** antwortet der Service mit 403 Forbidden

#### Requirement: Session-Handling im Frontend
Das Frontend SHALL das JWT nach Login in `sessionStorage` speichern, es per HTTP-Interceptor an alle
API-Requests anhaengen und bei 401-Antworten die Session loeschen und zur Login-Seite umleiten.

##### Scenario: Navigation ohne gueltige Session
- **GIVEN** kein gespeichertes Token (oder ein abgelaufenes Token)
- **WHEN** eine geschuetzte Route aufgerufen wird
- **THEN** leitet der Route Guard zur Login-Seite um (mit `returnUrl`)

##### Scenario: Automatischer Logout bei 401
- **GIVEN** eine gespeicherte Session
- **WHEN** ein API-Request mit 401 antwortet
- **THEN** wird die Session geloescht und zur Login-Seite umgeleitet

#### Requirement: Brute-Force-Schutz auf dem Login-Endpoint
Der Login-Endpoint SHALL die Anzahl der Versuche pro Client-IP und Zeitfenster begrenzen (Rate Limiting).

##### Scenario: Zu viele Login-Versuche
- **GIVEN** mehr als 5 Login-Versuche derselben IP innerhalb einer Minute
- **WHEN** ein weiterer Login-Versuch erfolgt
- **THEN** antwortet der Service mit 429 Too Many Requests

## 4. UI/UX Requirements
- **Komponenten**: `LoginComponent` (Standalone, Reactive Forms, DaisyUI `card`/`input`/`btn`) in
  `customer-web` und `employee-web`; Logout-Button + angezeigter Nutzername im App-Shell-Header.
- **State**: `AuthStateService` (Signals: `currentUser`, `isAuthenticated`, `isAdmin`, `loading`, `error`),
  `AuthTokenStorage` (sessionStorage-Wrapper).
- **i18n / a11y**: Keys unter `auth.*` (DE/EN, siehe `public/i18n/{de,en}.json`), Formularfelder mit
  `label[for]`, `aria-invalid`/`aria-describedby`, Fehlermeldungen mit `role="alert"`.

## 5. API & Backend Requirements
- **Authentifizierung/Autorisierung**: JWT Bearer (HS256, symmetrischer Signing-Key), Claims
  `sub`/`ClaimTypes.NameIdentifier`, `ClaimTypes.Role`, `ClaimTypes.Email`, `ClaimTypes.Name`.
  Rollen `Employee`/`Admin` gemaess `kundenverwaltung.openspec.md`/`mitarbeiterverwaltung.openspec.md`
  Abschnitt 5 (unveraendert, nur jetzt technisch verifizierbar).
- **Endpoints**:
  - `POST /api/auth/login`: `{ email, password }` -> `{ accessToken, expiresAtUtc, userId, role, displayName }`
    (`identity-service`, ueber Gateway `/api/auth/**` anonym erreichbar).
- **Modelle/Entitäten**: `User` (Id, Email, PasswordHash, Role, DisplayName) - bewusst **kein** PostgreSQL-Store
  (`identity_db`) in diesem Entwicklungsstand; In-Memory-Seed pro Prozessstart (siehe Abschnitt 7).

## 6. KI/AI Requirements (Optional)
- Nicht relevant fuer diese Change-Spec.

## 7. Design (Technischer Ansatz)
- **Kein OIDC-Discovery/Authority**: Da `identity-service` ein schlanker, selbst gehosteter JWT-Issuer ist
  (kein vollwertiges OAuth2/OIDC-Produkt, kein externer IdP wie Auth0/Keycloak), validieren Gateway/customer-service/
  employee-service das Token direkt gegen einen symmetrischen Signing-Key statt gegen einen Metadaten-Endpoint.
  Issuer/Audience/SigningKey muessen deshalb explizit konsistent konfiguriert werden (siehe AppHost.cs,
  Aspire-Parameter `jwt-issuer`/`jwt-audience`/`jwt-signing-key`).
- **Defense in Depth**: Das Gateway terminiert Auth (Policy `gateway-authenticated`) fuer `/api/customers/**`
  und `/api/employees/**`, leitet den `Authorization`-Header aber unveraendert an die Downstream-Services
  weiter (kein Token-Neuausstellen im Gateway). customer-service/employee-service validieren das Token
  eigenstaendig erneut - kein reines Vertrauen ins Gateway (ADR 0001 Abschnitt 4).
- **Kein `identity_db`**: Fuer zwei Seed-Testnutzer wird bewusst keine ASP.NET Core Identity + EF Core-
  Persistenzschicht eingefuehrt (kein Gold-Plating). Passwoerter werden dennoch nie im Klartext gehalten:
  `PasswordHasher<T>` hasht die aus Konfiguration/Environment-Variablen kommenden Klartext-Seed-Werte beim
  Start; ist kein Seed-Passwort konfiguriert, wird in Development ein zufaelliges Passwort generiert und
  einmalig geloggt (kein Hardcoding, kein Produktionsrisiko).
- **sessionStorage statt localStorage**: begrenzt das Zeitfenster eines per XSS gestohlenen Tokens auf die
  aktuelle Tab-Session (siehe `AuthTokenStorage`-Kommentar). HttpOnly-Cookies waeren staerker, sind aber fuer
  diesen Entwicklungsstand mit Cross-Origin-Gateway/Services nicht vorgesehen.
- **Offene Punkte (bewusst vereinfacht)**: keine Refresh-Token-Rotation (Access Token laeuft nach 60 Minuten
  ab, Nutzer muss sich neu anmelden), keine echte Entra-ID-Föderation/kein externer IdP, kein `identity_db`.

## 8. Tasks (Implementierungs-Checkliste)
- [x] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/IdentityService.Tests`,
      `Tests/CustomerWeb.Tests` (Ergaenzung um `auth-state.service.spec.ts`, `auth.guard.spec.ts`)
- [x] 2. Backend implementieren (`identity-service` Domain/Application/Infrastructure/Web; Gateway/
      customer-service/employee-service Auth-Konfiguration aktualisiert)
- [x] 3. Frontend implementieren (Login, Interceptor, Guard, i18n `auth.*`) in `customer-web` und `employee-web`
- [x] 4. E2E-Tests (QA Agent) geschrieben UND vollstaendig gruen verifiziert (qa-agent, 2026-09-30, erneut bestaetigt 2026-10-01 nach Bug-5/6/7-Fixes) — `Tests/E2E/tests/auth.spec.ts` (6 Tests: Login Admin/Employee erfolgreich, falsches Passwort, unbekannte E-Mail, Navigation-ohne-Session-Redirect, automatischer Logout bei 401). **6 von 6 Tests gruen** (zuletzt erneut real verifiziert 2026-10-01), real gegen den laufenden AppHost + `Clients/crm-web` verifiziert (mehrfach wiederholt, stabil) — der zuvor durch Bug 1 (fehlende CORS-Middleware) blockierte 401-Logout-Test laeuft jetzt vollstaendig durch, da Bug 1 behoben ist. Zusaetzlich verifiziert: Brute-Force-Rate-Limiting (429) funktioniert korrekt (in dieser Session mehrfach reproduziert; die E2E-Suite kompensiert dies inzwischen per Retry-mit-Wartezeit in `fixtures/auth.ts`/`fixtures/api.ts`, siehe `Tests/E2E/README.md`).
- [ ] 5. Doku/OpenAPI aktualisiert (Swagger fuer `identity-service` ist vorhanden, README/Systemuebersicht
      noch durch `doc-agent` zu ergaenzen)

## 9. Acceptance Criteria (DoD)
- [x] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und gruen
      (`Tests/IdentityService.Tests`: 7 Tests; `Tests/CustomerService.Tests`/`Tests/EmployeeService.Tests`:
      bestehende RBAC-/Ownership-Tests bleiben gruen; `Tests/CustomerWeb.Tests`: 5 neue Tests).
- [x] Backend API (C# 14) ist implementiert und getestet (TUnit): 82 Tests gruen
      (35 CustomerService + 40 EmployeeService + 7 IdentityService).
- [x] Angular 22 Komponente ist implementiert und gestylt (DaisyUI 5) in beiden Clients.
- [x] E2E Tests (QA Agent) sind gruen (siehe Task 4, 6/6 gruen).
- [x] Swagger/OpenAPI fuer `identity-service` ist vorhanden (analog zu den anderen Services).
- [ ] Spec ist archiviert - verbleibt `in-progress`, bis QA/Doc-Agent Tasks 4/5 abgeschlossen haben.
