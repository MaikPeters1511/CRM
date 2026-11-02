# Change: Geräte-Vertrauen & Admin-Sicherheitsrichtlinie

> **Status:** proposed
> Ablage: `UserStories/identity-device-trust.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-identity-device-trust.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Selbst mit MFA (`identity-mfa-totp.openspec.md`) und Passkeys (`identity-passkeys-webauthn.openspec.md`) bleibt ein Risiko: ein Login von einem komplett unbekannten, nie zuvor genutzten Gerät kann auf ein kompromittiertes Credential hindeuten. Zusätzlich fehlt der Organisation aktuell jede zentrale Steuerungsmöglichkeit (MFA-Pflicht, Passwort-Richtlinie, Account-Lockout) über alle Nutzer hinweg — jede Sicherheitseinstellung ist bisher rein individuell.
- **What Changes:**
  - Bei Login von unbekanntem Gerät: zusätzliche Bestätigung per E-Mail-Code, bevor der Login abgeschlossen wird.
  - Admins erhalten Einsicht in bekannte Geräte je Mitarbeiter und können Geräte sperren/freigeben.
  - Admins können eine org-weite Sicherheitsrichtlinie pflegen: MFA-Pflicht, Passwort-Mindestlänge/-Ablauf, Account-Lockout-Schwelle.
  - Passwort-Richtlinie wird beim Setzen/Ändern von Passwörtern durchgesetzt; Account-Lockout wird beim Login nach n Fehlversuchen durchgesetzt.
- **Impact:** Betrifft die Capability-Spec `UserStories/specs/identity.openspec.md` (wird nach Abschluss/Archivierung aktualisiert, nicht jetzt). Layer: Domain (`KnownDevice`, `PendingDeviceConfirmation`, `SecurityPolicy`), Application (Device-Fingerprinting-/Confirmation-Use-Cases, Policy-Durchsetzung beim Passwort-Setzen und Login), Infrastructure (`IDeviceConfirmationNotifier`-Implementierung, EF Core gegen `identity_db`), Presentation (`identity-service`- und Admin-Endpunkte). Setzt die in `identity-mfa-totp.openspec.md` Abschnitt 7 eingeführte `identity_db`-Infrastruktur voraus; das bereits bestehende Requirement "Brute-Force-Schutz auf dem Login-Endpoint" (IP-basiertes Rate Limiting, `UserStories/specs/identity.openspec.md`) bleibt unverändert und wird durch den hier neu eingeführten nutzerbasierten Account-Lockout ergänzt, nicht ersetzt.

## 2. User Stories
- Als Mitarbeiter möchte ich bei einem Login von einem neuen, mir noch nicht bekannten Gerät eine zusätzliche Bestätigung per E-Mail erhalten, damit ein Angreifer mit meinem Passwort allein nicht von einem fremden Gerät auf mein Konto zugreifen kann.
- Als Admin möchte ich sehen, welche Geräte für einen Mitarbeiter als bekannt gelten, damit ich bei einem Sicherheitsvorfall nachvollziehen kann, von wo aus Zugriffe erfolgt sind.
- Als Admin möchte ich ein verdächtiges Gerät sperren können, damit zukünftige Logins von diesem Gerät verhindert werden.
- Als Admin möchte ich org-weit MFA verpflichtend machen und eine Passwort-Richtlinie definieren können, damit die Sicherheitsstandards nicht von der individuellen Entscheidung einzelner Mitarbeiter abhängen.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Erkennung unbekannter Geräte beim Login
Das System SHALL bei jedem erfolgreichen Passwort- (bzw. Passkey-)Login einen Geräte-Fingerprint aus User-Agent und IP-Adresse bilden und gegen die für den Nutzer bekannten Geräte (`KnownDevice`) abgleichen.

##### Scenario: Login von bekanntem Gerät
- **GIVEN** ein Nutzer mit einem bereits als bekannt hinterlegten Geräte-Fingerprint (gleicher User-Agent + IP-Bereich wie bei einem früheren erfolgreichen Login)
- **WHEN** der Nutzer sich erneut von diesem Gerät anmeldet
- **THEN** wird der Login ohne zusätzliche Geräte-Bestätigung abgeschlossen (sofern keine MFA-Pflicht entgegensteht)

##### Scenario: Login von unbekanntem Gerät
- **GIVEN** ein Nutzer, dessen aktueller Geräte-Fingerprint noch nicht als `KnownDevice` hinterlegt ist
- **WHEN** der Nutzer sich mit korrektem Passwort (und ggf. korrektem MFA-Code) anmeldet
- **THEN** antwortet der Service nicht mit einem Access Token, sondern mit Status `DeviceConfirmationRequired`; ein 6-stelliger Bestätigungscode wird über `IDeviceConfirmationNotifier` an die registrierte E-Mail-Adresse des Nutzers versendet

#### Requirement: Geräte-Bestätigung abschließen
Das System SHALL den Login erst nach erfolgreicher Bestätigung des per E-Mail versendeten Codes über `POST /api/auth/devices/confirm` abschließen und das Gerät danach als bekannt hinterlegen.

##### Scenario: Erfolgreiche Geräte-Bestätigung
- **GIVEN** ein gültiger, noch nicht abgelaufener Bestätigungscode aus einem vorherigen Login-Versuch von unbekanntem Gerät
- **WHEN** `POST /api/auth/devices/confirm` mit passendem Code aufgerufen wird
- **THEN** antwortet der Service mit 200 OK und liefert das vollständige JWT; das Gerät wird als `KnownDevice` für zukünftige Logins hinterlegt

##### Scenario: Falscher oder abgelaufener Bestätigungscode
- **GIVEN** eine ausstehende Geräte-Bestätigung
- **WHEN** `POST /api/auth/devices/confirm` mit falschem oder abgelaufenem Code aufgerufen wird
- **THEN** antwortet der Service mit 401 Unauthorized, kein Token wird ausgestellt, das Gerät bleibt unbestätigt

#### Requirement: Admin-Einsicht in Mitarbeiter-Geräte
Das System SHALL Admins über `GET /api/admin/users/{userId}/devices` eine Liste der bekannten Geräte eines Mitarbeiters liefern.

##### Scenario: Admin sieht bekannte Geräte eines Mitarbeiters
- **GIVEN** ein Mitarbeiter mit zwei bekannten Geräten
- **WHEN** ein Admin `GET /api/admin/users/{userId}/devices` aufruft
- **THEN** antwortet der Service mit 200 OK und liefert beide Geräte inkl. User-Agent, letzter IP-Bereich, Zeitpunkt der Erstbestätigung und letzter Nutzung sowie Sperrstatus

##### Scenario: Nicht-Admin versucht Geräteliste einzusehen
- **GIVEN** ein eingeloggter Mitarbeiter ohne Admin-Rolle
- **WHEN** `GET /api/admin/users/{userId}/devices` aufgerufen wird
- **THEN** antwortet der Service mit 403 Forbidden

#### Requirement: Gerät sperren/freigeben
Das System SHALL Admins über `POST /api/admin/users/{userId}/devices/{deviceId}/revoke` erlauben, ein bekanntes Gerät zu sperren; ein gesperrtes Gerät gilt beim nächsten Login wieder als unbekannt.

##### Scenario: Admin sperrt ein Gerät
- **GIVEN** ein bekanntes, nicht gesperrtes Gerät eines Mitarbeiters
- **WHEN** ein Admin `POST /api/admin/users/{userId}/devices/{deviceId}/revoke` aufruft
- **THEN** antwortet der Service mit 200 OK, das Gerät gilt als gesperrt

##### Scenario: Login von zuvor gesperrtem Gerät
- **GIVEN** ein von einem Admin gesperrtes Gerät
- **WHEN** sich der Mitarbeiter erneut von diesem Gerät anmeldet
- **THEN** durchläuft der Login erneut die Geräte-Bestätigung per E-Mail-Code wie bei einem unbekannten Gerät (kein automatischer Zugriff über das gesperrte Gerät)

#### Requirement: Org-weite Sicherheitsrichtlinie verwalten
Das System SHALL Admins über `GET /api/admin/security-policy` und `PUT /api/admin/security-policy` erlauben, eine org-weite Sicherheitsrichtlinie (MFA-Pflicht, Passwort-Mindestlänge, Passwort-Ablauf in Tagen, Lockout-Schwelle nach Fehlversuchen) einzusehen und zu ändern.

##### Scenario: Admin aktiviert org-weite MFA-Pflicht
- **GIVEN** eine bestehende Sicherheitsrichtlinie mit `RequireMfa = false`
- **WHEN** ein Admin `PUT /api/admin/security-policy` mit `{ requireMfa: true, ... }` aufruft
- **THEN** antwortet der Service mit 200 OK; ab sofort greift für alle Nutzer ohne eigene aktive MFA bei nächstem Login das in `identity-mfa-totp.openspec.md` beschriebene `MfaRequired`-Verhalten de facto als Pflicht (betroffene Nutzer müssen MFA einrichten, bevor sie sich vollständig anmelden können — konkrete Erzwingung des Einrichtungszwangs ist Teil des Login-Flows aus `identity-mfa-totp.openspec.md`)

##### Scenario: Nicht-Admin versucht Richtlinie zu ändern
- **GIVEN** ein eingeloggter Mitarbeiter ohne Admin-Rolle
- **WHEN** `PUT /api/admin/security-policy` aufgerufen wird
- **THEN** antwortet der Service mit 403 Forbidden

#### Requirement: Durchsetzung der Passwort-Richtlinie
Das System SHALL beim Setzen oder Ändern eines Passworts die aktuelle Passwort-Richtlinie (Mindestlänge) prüfen und bei Verstoß ablehnen.

##### Scenario: Passwort zu kurz
- **GIVEN** eine Sicherheitsrichtlinie mit Mindestlänge 10
- **WHEN** ein Nutzer ein neues Passwort mit weniger als 10 Zeichen setzen möchte
- **THEN** antwortet der Service mit 400 Bad Request, das Passwort wird nicht übernommen

##### Scenario: Passwort erfüllt Richtlinie
- **GIVEN** eine Sicherheitsrichtlinie mit Mindestlänge 10
- **WHEN** ein Nutzer ein neues Passwort mit mindestens 10 Zeichen setzt
- **THEN** antwortet der Service mit 200 OK, das Passwort wird übernommen

#### Requirement: Account-Lockout nach Fehlversuchen
Das System SHALL einen Nutzer-Account nach Erreichen der konfigurierten Lockout-Schwelle an aufeinanderfolgenden fehlgeschlagenen Login-Versuchen vorübergehend sperren, unabhängig vom IP-basierten Rate Limiting aus `UserStories/specs/identity.openspec.md`.

##### Scenario: Account wird nach n Fehlversuchen gesperrt
- **GIVEN** eine Lockout-Schwelle von 5 Fehlversuchen
- **WHEN** fünf aufeinanderfolgende `POST /api/auth/login`-Versuche mit falschem Passwort für denselben Nutzer erfolgen
- **THEN** antwortet der Service beim sechsten Versuch (auch mit korrektem Passwort) mit 423 Locked, bis die konfigurierte Sperrzeit abgelaufen ist oder ein Admin den Account entsperrt

##### Scenario: Erfolgreicher Login setzt Fehlversuchszähler zurück
- **GIVEN** zwei vorherige fehlgeschlagene Login-Versuche für einen Nutzer, Lockout-Schwelle 5
- **WHEN** ein anschließender Login-Versuch mit korrektem Passwort erfolgt
- **THEN** wird der Login fortgesetzt (ggf. mit MFA-/Geräte-Bestätigung) und der Fehlversuchszähler wird auf 0 zurückgesetzt

## 4. UI/UX Requirements
Frontend-Umsetzung erfolgt in separater Session, hier nur Backend-Vertrag/API.

## 5. API & Backend Requirements
- **Authentifizierung/Autorisierung**: `POST /api/auth/devices/confirm` ist `[AllowAnonymous]` (Teil des Login-Flows, Identität über das temporäre Bestätigungs-Token). `GET/POST /api/admin/users/{userId}/devices/**` und `GET/PUT /api/admin/security-policy` erfordern Rolle `Admin` (`[Authorize(Roles = "Admin")]`).
- **Endpoints**:
  - `POST /api/auth/devices/confirm`: `{ deviceConfirmationToken, code }` → `{ accessToken, expiresAtUtc, userId, role, displayName }` / 401 Unauthorized.
  - `GET /api/admin/users/{userId}/devices`: → `[{ id, userAgent, lastIpRange, firstConfirmedAtUtc, lastUsedAtUtc, isRevoked }]`.
  - `POST /api/admin/users/{userId}/devices/{deviceId}/revoke`: → 200 OK / 404 Not Found.
  - `GET /api/admin/security-policy`: → `{ requireMfa, minPasswordLength, passwordExpiryDays, lockoutThreshold, lockoutDurationMinutes }`.
  - `PUT /api/admin/security-policy`: gleiches Schema wie GET-Response als Request → 200 OK.
- **Modelle/Entitäten (PostgreSQL, `identity_db`)**:
  - `KnownDevice`: `Id` (Guid), `UserId` (Guid, FK), `FingerprintHash` (string, Hash aus User-Agent + IP-Bereich), `UserAgent` (string), `LastIpRange` (string), `FirstConfirmedAtUtc` (DateTime), `LastUsedAtUtc` (DateTime), `IsRevoked` (bool).
  - `PendingDeviceConfirmation`: `Id` (Guid), `UserId` (Guid, FK), `FingerprintHash` (string), `CodeHash` (string, gehasht), `ExpiresAtUtc` (DateTime), `CreatedAtUtc` (DateTime).
  - `SecurityPolicy` (org-weit, Singleton-Zeile): `Id` (Guid, fest), `RequireMfa` (bool), `MinPasswordLength` (int), `PasswordExpiryDays` (int?), `LockoutThreshold` (int), `LockoutDurationMinutes` (int).
  - Erweiterung `User` (aus `identity-mfa-totp.openspec.md`): `FailedLoginAttempts` (int), `LockedUntilUtc` (DateTime?), `PasswordChangedAtUtc` (DateTime).

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Nein.
- **Prompt/System Message**: Nicht relevant.

## 7. Design (Technischer Ansatz)
- **Geräte-Fingerprinting:** Pragmatischer Hash aus User-Agent-String und einer IP-Adress-Bucket-Bildung (z.B. /24-Subnetz statt exakter IP, um gängige ISP-Adresswechsel nicht ständig als "neues Gerät" zu werten); kein Browser-Fingerprinting mit JS-Canvas/Device-APIs, kein Overengineering. Dies ist eine bewusste Vereinfachung — siehe unten.
- **`IDeviceConfirmationNotifier`-Abstraktion:** Application-Interface analog zum bestehenden `IReminderNotifier`-Pattern aus `appointment-service` (`Services/appointment-service/src/AppointmentService.Application/Abstractions/IReminderNotifier.cs`). Infrastructure-Implementierung in dieser Iteration: `LogDeviceConfirmationNotifier`, der den Code lediglich strukturiert loggt (analog `LogReminderNotifier`), **kein** tatsächlicher E-Mail-Versand, da aktuell kein SMTP-Provider im Projekt konfiguriert ist.
- **Account-Lockout vs. IP-Rate-Limiting:** Ergänzt das bestehende IP-basierte Rate Limiting aus `UserStories/specs/identity.openspec.md` ("Brute-Force-Schutz auf dem Login-Endpoint"); beide Mechanismen sind unabhängig und wirken zusammen (Defense in Depth): IP-Limiting schützt vor verteilten Angriffen über viele Accounts, Account-Lockout schützt den einzelnen Account unabhängig von der Angreifer-IP.
- **Voraussetzung:** Baut auf der in `identity-mfa-totp.openspec.md` Abschnitt 7 eingeführten `identity_db`-Infrastruktur auf.
- **Bewusste Vereinfachung (KNOWN SIMPLIFICATION):** `IDeviceConfirmationNotifier` wird in dieser Iteration nur als Log-Stub implementiert (kein SMTP). Die Geräte-Bestätigung ist damit im aktuellen Deployment nur über Log-Einsicht (Entwickler/Admin) nachvollziehbar, nicht durch den Endnutzer per echter E-Mail. Produktionsreife erfordert einen Folge-Change (SMTP-Anbindung, z.B. über `devops-agent`/`sec-agent`-Abstimmung), der hier bewusst nicht vorgezogen wird, um den Scope dieser Spec auf das Identity-Verhalten zu begrenzen.
- **Bewusste Vereinfachung:** Geräte-Fingerprint basiert auf User-Agent + IP-Bucket statt eines robusten Client-Fingerprinting-Verfahrens; dies kann zu gelegentlichen False Positives (unnötige erneute Bestätigung) oder seltenen False Negatives (z.B. zwei Geräte mit identischem User-Agent im gleichen Subnetz) führen, wird aber als für ein internes CRM ausreichend bewertet.

## 8. Tasks (Implementierungs-Checkliste)
- [ ] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/IdentityService.Tests`
- [ ] 2. Entitäten `KnownDevice`, `PendingDeviceConfirmation`, `SecurityPolicy` sowie `User`-Erweiterung + EF-Core-Migration gegen `identity_db` implementieren
- [ ] 3. Geräte-Fingerprinting und Abgleich im Login-Flow implementieren (Domain → Application → Infrastructure → Web)
- [ ] 4. `IDeviceConfirmationNotifier` + `LogDeviceConfirmationNotifier`-Stub implementieren, `POST /api/auth/devices/confirm` implementieren
- [ ] 5. Admin-Endpunkte für Geräteliste/-sperrung implementieren
- [ ] 6. `SecurityPolicy`-Verwaltungsendpunkte implementieren, Passwort-Richtlinien-Durchsetzung beim Passwort-Setzen/Ändern verdrahten
- [ ] 7. Account-Lockout-Logik im Login-Flow implementieren (Zähler, Sperrzeit, Reset bei Erfolg)
- [ ] 8. Doku/OpenAPI aktualisiert

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün.
- [ ] Backend API (C# 14) ist implementiert und getestet (TUnit) in `Tests/IdentityService.Tests`.
- [ ] `IDeviceConfirmationNotifier`-Abstraktion vorhanden, Log-Stub-Implementierung nachweisbar, KNOWN SIMPLIFICATION im finalen Dokument erhalten.
- [ ] Admin-only-Endpunkte durch RBAC-Tests abgedeckt (403 für Nicht-Admins).
- [ ] Swagger/OpenAPI Compliance ist gegeben.
- [ ] Spec ist archiviert (Status `archived`, Requirements in `UserStories/specs/identity.openspec.md` übernommen).
