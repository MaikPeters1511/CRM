# Change: Zwei-Faktor-Authentifizierung (TOTP)

> **Status:** proposed
> Ablage: `UserStories/identity-mfa-totp.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-identity-mfa-totp.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Der `identity-service` authentifiziert aktuell ausschließlich per Passwort (siehe `UserStories/specs/identity.openspec.md`). Ein kompromittiertes Passwort reicht damit für vollen Account-Zugriff. Für ein CRM mit Kunden- und Mitarbeiterdaten ist eine zweite Faktor-Stufe (OWASP-Empfehlung) notwendig, bevor weitere Sicherheitsfeatures (Passkeys, Device Trust, Audit Log) sinnvoll aufgebaut werden können.
- **What Changes:**
  - Neue MFA-Verwaltung im eigenen Profil: TOTP-Secret einrichten, per Code aktivieren, deaktivieren.
  - Login-Flow wird zweistufig, sobald MFA für den Nutzer (eigene Einstellung oder org-weite Pflicht, siehe `identity-device-trust.openspec.md`) aktiv ist.
  - **BREAKING** (intern, nicht öffentlich API-kompatibel): `identity-service` erhält erstmals eine eigene PostgreSQL-Datenbank (`identity_db`) via EF Core; die bisherige In-Memory-Nutzerliste wird durch DB-persistierte User-Entitäten abgelöst (Seed-Daten bleiben für Dev/Test erhalten). Diese Infrastruktur-Grundlage wird hier einmalig eingeführt, da die drei weiteren Changes (`identity-passkeys-webauthn`, `identity-device-trust`, `identity-audit-log`) ebenfalls persistente Entitäten benötigen und darauf aufbauen.
- **Impact:** Betrifft die Capability-Spec `UserStories/specs/identity.openspec.md` (wird nach Abschluss/Archivierung dieser Change aktualisiert, nicht jetzt). Layer: Domain (neue Entität `MfaCredential`, erweiterter `User`), Application (Use Cases Setup/Verify/Disable, zweistufiger Login-Use-Case), Infrastructure (EF Core/`identity_db`, OtpNet-Integration), Presentation (`identity-service` Minimal-API-Endpunkte).

## 2. User Stories
- Als Mitarbeiter möchte ich in meinem Profil TOTP-basierte Zwei-Faktor-Authentifizierung einrichten, damit mein Account auch bei einem gestohlenen Passwort geschützt ist.
- Als Mitarbeiter möchte ich beim Login nach korrektem Passwort einen TOTP-Code eingeben müssen, wenn MFA aktiv ist, damit ich sicher sein kann, dass ein gestohlenes Passwort allein nicht reicht.
- Als Mitarbeiter möchte ich MFA wieder deaktivieren können (mit erneutem Nachweis per Code oder Passwort), damit ich bei Verlust des Authenticator-Geräts nicht ausgesperrt bleibe (Reaktivierung danach erneut nötig).
- Als Admin möchte ich mich darauf verlassen können, dass Secrets nie im Klartext geloggt oder unverschlüsselt in der DB liegen, damit ein DB-Leak nicht automatisch alle Accounts kompromittiert.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: MFA-Setup (Secret-Generierung)
Das System SHALL für einen eingeloggten Nutzer ohne aktive MFA einen `POST /api/auth/mfa/setup`-Endpoint bereitstellen, der ein neues TOTP-Secret generiert und als `otpauth://`-URI sowie Rohsecret (Base32) zurückgibt, ohne es bereits zu aktivieren.

##### Scenario: Secret wird generiert
- **GIVEN** ein eingeloggter Mitarbeiter ohne aktive MFA
- **WHEN** `POST /api/auth/mfa/setup` aufgerufen wird
- **THEN** antwortet der Service mit 200 OK und liefert ein neues Base32-Secret sowie eine `otpauth://totp/...`-URI; der Mitarbeiter gilt weiterhin als MFA-inaktiv

##### Scenario: Setup bei bereits aktiver MFA
- **GIVEN** ein eingeloggter Mitarbeiter mit bereits aktiver MFA
- **WHEN** `POST /api/auth/mfa/setup` erneut aufgerufen wird
- **THEN** antwortet der Service mit 409 Conflict (zuerst deaktivieren, dann neu einrichten)

#### Requirement: MFA-Aktivierung per Verifizierungscode
Das System SHALL MFA erst nach erfolgreicher Verifizierung eines ersten TOTP-Codes über `POST /api/auth/mfa/verify` aktivieren.

##### Scenario: Erfolgreiche Aktivierung
- **GIVEN** ein zuvor generiertes, noch nicht aktiviertes Secret für den eingeloggten Nutzer
- **WHEN** `POST /api/auth/mfa/verify` mit einem zum Secret passenden, aktuell gültigen 6-stelligen TOTP-Code aufgerufen wird
- **THEN** antwortet der Service mit 200 OK, MFA ist für den Nutzer ab sofort aktiv

##### Scenario: Aktivierung mit falschem Code
- **GIVEN** ein zuvor generiertes, noch nicht aktiviertes Secret
- **WHEN** `POST /api/auth/mfa/verify` mit einem falschen oder abgelaufenen Code aufgerufen wird
- **THEN** antwortet der Service mit 400 Bad Request, MFA bleibt inaktiv

#### Requirement: MFA-Deaktivierung
Das System SHALL MFA-Deaktivierung über `DELETE /api/auth/mfa` nur nach erneutem Nachweis (gültiger aktueller TOTP-Code ODER aktuelles Passwort) zulassen.

##### Scenario: Deaktivierung mit gültigem Code
- **GIVEN** ein Mitarbeiter mit aktiver MFA
- **WHEN** `DELETE /api/auth/mfa` mit einem gültigen aktuellen TOTP-Code aufgerufen wird
- **THEN** antwortet der Service mit 200 OK, MFA ist deaktiviert, das Secret wird aus der DB entfernt

##### Scenario: Deaktivierung ohne Nachweis
- **GIVEN** ein Mitarbeiter mit aktiver MFA
- **WHEN** `DELETE /api/auth/mfa` ohne gültigen Code und ohne korrektes Passwort aufgerufen wird
- **THEN** antwortet der Service mit 403 Forbidden, MFA bleibt aktiv

#### Requirement: Zweistufiger Login bei aktiver MFA
Das System SHALL bei Nutzern mit aktiver MFA (eigene Einstellung oder org-weite Pflicht) nach korrektem Passwort keinen Token ausstellen, sondern einen Zwischenstatus `MfaRequired` mit kurzlebigem MFA-Challenge-Token liefern; der Login wird erst über `POST /api/auth/login/mfa` mit gültigem TOTP-Code abgeschlossen.

##### Scenario: Login mit aktiver MFA, Schritt 1
- **GIVEN** ein Nutzer mit aktiver MFA und korrektem Passwort
- **WHEN** `POST /api/auth/login` aufgerufen wird
- **THEN** antwortet der Service mit 200 OK und Status `MfaRequired` plus einem kurzlebigen Challenge-Token, aber ohne Access Token

##### Scenario: Login mit aktiver MFA, Schritt 2 erfolgreich
- **GIVEN** ein gültiges MFA-Challenge-Token aus Schritt 1
- **WHEN** `POST /api/auth/login/mfa` mit passendem, aktuell gültigem TOTP-Code aufgerufen wird
- **THEN** antwortet der Service mit 200 OK und liefert das vollständige JWT (Claims wie im bestehenden Login)

##### Scenario: Login mit aktiver MFA, Schritt 2 mit falschem Code
- **GIVEN** ein gültiges MFA-Challenge-Token
- **WHEN** `POST /api/auth/login/mfa` mit falschem Code aufgerufen wird
- **THEN** antwortet der Service mit 401 Unauthorized, kein Token wird ausgestellt

##### Scenario: Login ohne aktive MFA bleibt unverändert
- **GIVEN** ein Nutzer ohne aktive MFA und ohne org-weite MFA-Pflicht
- **WHEN** `POST /api/auth/login` mit korrekten Anmeldedaten aufgerufen wird
- **THEN** antwortet der Service wie bisher direkt mit 200 OK und vollständigem JWT (keine Verhaltensänderung)

#### Requirement: Keine Secret-Exposition in Logs
Das System SHALL TOTP-Secrets und eingegebene Codes niemals im Klartext in Log-Ausgaben schreiben.

##### Scenario: Fehlgeschlagene Verifizierung wird geloggt
- **GIVEN** ein fehlgeschlagener Verifizierungsversuch (Setup oder Login-Schritt 2)
- **WHEN** die Anwendung diesen Vorfall protokolliert
- **THEN** enthält der Log-Eintrag weder das Secret noch den eingegebenen Code im Klartext

## 4. UI/UX Requirements
Frontend-Umsetzung erfolgt in separater Session, hier nur Backend-Vertrag/API. Hinweis für die spätere Frontend-Session: QR-Code-Rendering der `otpauth://`-URI ist Frontend-Sache (z.B. via Client-Library), der Service liefert ausschließlich Rohdaten.

## 5. API & Backend Requirements
- **Authentifizierung/Autorisierung**: `POST /api/auth/mfa/setup`, `POST /api/auth/mfa/verify`, `DELETE /api/auth/mfa` erfordern gültiges JWT des betroffenen Nutzers (`[Authorize]`, kein Rollenerfordernis, nur Self-Service). `POST /api/auth/login` und `POST /api/auth/login/mfa` bleiben `[AllowAnonymous]` (wie bisheriger Login).
- **Endpoints**:
  - `POST /api/auth/mfa/setup`: Request leer (Identität aus JWT) → `{ secretBase32, otpauthUri }`.
  - `POST /api/auth/mfa/verify`: `{ code }` → 200 OK / 400 Bad Request.
  - `DELETE /api/auth/mfa`: `{ code? , password? }` (mind. eines erforderlich) → 200 OK / 403 Forbidden.
  - `POST /api/auth/login`: unverändertes Request-Schema `{ email, password }`; Response erweitert um `{ status: "Authenticated" | "MfaRequired", accessToken?, mfaChallengeToken? }`.
  - `POST /api/auth/login/mfa`: `{ mfaChallengeToken, code }` → `{ accessToken, expiresAtUtc, userId, role, displayName }` (identisch zum bisherigen Erfolgs-Response von `/login`).
- **Modelle/Entitäten (PostgreSQL, `identity_db`)**:
  - `User` (erweitert gegenüber bisherigem In-Memory-Modell): `Id` (Guid), `Email` (string, unique), `DisplayName` (string), `PasswordHash` (string), `Role` (string/Enum), `IsMfaEnabled` (bool), `CreatedAtUtc` (DateTime).
  - `MfaCredential`: `Id` (Guid), `UserId` (Guid, FK), `SecretEncrypted` (string, verschlüsselt/gehasht, nie Klartext), `IsActivated` (bool), `CreatedAtUtc` (DateTime), `ActivatedAtUtc` (DateTime?).

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Nein.
- **Prompt/System Message**: Nicht relevant.

## 7. Design (Technischer Ansatz)
- **PostgreSQL-Einführung für `identity-service` (Grundlage für alle 4 Changes dieser Sicherheits-Initiative):**
  - Neue Datenbank `identity_db`, EF Core `DbContext` (`IdentityDbContext`) analog zum Muster in `employee-service`.
  - Migrationen werden via Hosted Service (`IdentityDbMigrationHostedService` o.ä.) beim Start automatisch angewendet — gleiches Muster wie in `employee-service` (siehe dortige Infrastructure-Schicht als Referenz).
  - Migration der bisherigen zwei In-Memory-Testnutzer (Admin/Employee) zu DB-Seed-Daten: beim ersten Start wird geprüft, ob die `Users`-Tabelle leer ist; falls ja, werden die zwei bekannten Testnutzer mit identischen Anmeldedaten wie bisher eingefügt (Dev/Test-Kontinuität, keine Breaking Change für bestehende E2E-Tests `Tests/E2E/tests/auth.spec.ts`).
  - .NET Aspire AppHost wird um eine PostgreSQL-Ressource `identity_db` erweitert (`WithReference`, kein Shared-Schema mit anderen Services, analog ADR 0001 Abschnitt 1/7).
- **TOTP-Implementierung:** Nutzung der NuGet-Bibliothek **OtpNet** (RFC 6238-konform) in der Infrastructure-Schicht hinter einem Application-Interface (`ITotpProvider`); keine Eigenimplementierung des Algorithmus.
- **Secret-Speicherung:** Secret wird vor Persistierung mittels symmetrischer Verschlüsselung (z.B. `IDataProtectionProvider` / AES) verschlüsselt gespeichert, nicht nur gehasht (im Unterschied zu Passwörtern muss das Secret für die Verifizierung wiederherstellbar sein). Schlüsselmaterial über Environment Variable/Secret Manager, nicht hardcoded (siehe `CLAUDE.md`, Abschnitt Sicherheit).
- **MFA-Challenge-Token:** kurzlebiges (z.B. 5 Minuten), signiertes JWT mit eigenem `purpose`-Claim (`mfa_challenge`), das nur gegen `/api/auth/login/mfa` eingelöst werden kann; kein regulärer Access Token.
- **Risiko/Alternative:** Eine alternative zustandslose Challenge über verschlüsseltes Token statt Server-Side-Session vermeidet zusätzliche DB-Last und passt zum bisherigen schlanken JWT-Issuer-Ansatz.
- **Bewusste Vereinfachung:** QR-Code-Rendering (Bildgenerierung aus der `otpauth://`-URI) ist explizit **nicht** Teil dieses Backend-Changes — der Service liefert nur die Rohdaten (Secret + URI), die Darstellung erfolgt im Frontend in einer separaten Session.
- **Bewusste Vereinfachung:** Keine Backup-/Recovery-Codes in dieser ersten Iteration; Verlust des Authenticator-Geräts erfordert Deaktivierung via Passwort oder Admin-Intervention (Admin-seitiges Zurücksetzen ist nicht Teil dieser Spec, ggf. Folge-Change).

## 8. Tasks (Implementierungs-Checkliste)
- [ ] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/IdentityService.Tests`
- [ ] 2. `identity_db` EF Core Setup (DbContext, Migrationen, Hosted-Service-Migration, Aspire-Ressource) implementieren
- [ ] 3. Migration der In-Memory-Seed-Nutzer zu DB-Seed implementieren, bestehende Login-Tests weiterhin grün halten
- [ ] 4. `MfaCredential`-Entität, `ITotpProvider`/OtpNet-Integration, Setup/Verify/Disable-Use-Cases implementieren (Domain → Application → Infrastructure → Web)
- [ ] 5. Zweistufigen Login-Flow (`MfaRequired`, `/api/auth/login/mfa`, Challenge-Token) implementieren
- [ ] 6. Secret-Verschlüsselung und Log-Redaction verifizieren
- [ ] 7. Doku/OpenAPI aktualisiert

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün.
- [ ] Backend API (C# 14) ist implementiert und getestet (TUnit) in `Tests/IdentityService.Tests`.
- [ ] `identity_db` läuft über .NET Aspire AppHost, Migrationen werden automatisch angewendet.
- [ ] Bestehende Login-Tests (`Tests/E2E/tests/auth.spec.ts`) bleiben grün (keine Regression durch DB-Migration).
- [ ] Secrets sind verschlüsselt in der DB, kein Klartext-Logging nachweisbar (Code-Review durch `sec-agent`).
- [ ] Swagger/OpenAPI Compliance ist gegeben.
- [ ] Spec ist archiviert (Status `archived`, Requirements in `UserStories/specs/identity.openspec.md` übernommen).
