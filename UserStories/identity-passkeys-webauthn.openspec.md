# Change: Passkeys (WebAuthn/FIDO2)

> **Status:** proposed
> Ablage: `UserStories/identity-passkeys-webauthn.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-identity-passkeys-webauthn.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Passwort + TOTP (siehe `identity-mfa-totp.openspec.md`) ist sicherer als reines Passwort, bleibt aber phishing-anfällig (Codes können abgefragt werden) und reibungsvoll im Alltag. Passkeys (WebAuthn/FIDO2) bieten phishing-resistente, passwortlose Authentifizierung über Plattform-Authenticatoren (z.B. Windows Hello, Touch ID) oder Hardware-Keys und sind der aktuelle Industriestandard für starke Authentifizierung.
- **What Changes:**
  - Neuer, alternativer Login-Weg per Passkey, zusätzlich zum bestehenden Passwort-(+MFA-)Login; kein bestehender Flow wird entfernt.
  - Mitarbeiter können im eigenen Profil Passkeys registrieren und verwalten (auflisten, löschen).
- **Impact:** Betrifft die Capability-Spec `UserStories/specs/identity.openspec.md` (wird nach Abschluss/Archivierung aktualisiert, nicht jetzt). Layer: Domain (neue Entität `PasskeyCredential`), Application (Registrierungs-/Login-Ceremony-Use-Cases), Infrastructure (Fido2NetLib-Integration, EF Core Repository gegen `identity_db` aus `identity-mfa-totp.openspec.md`), Presentation (`identity-service` Minimal-API-Endpunkte). Setzt die in `identity-mfa-totp.openspec.md` Abschnitt 7 eingeführte `identity_db`/EF-Core-Infrastruktur voraus.

## 2. User Stories
- Als Mitarbeiter möchte ich mich ohne Passwort per Passkey (Fingerabdruck/Gesichtserkennung/Hardware-Key) anmelden können, damit ich nicht auf ein phishing-anfälliges Passwort angewiesen bin.
- Als Mitarbeiter möchte ich mehrere Passkeys registrieren können (z.B. Laptop und Smartphone), damit ich nicht von einem einzigen Gerät abhängig bin.
- Als Mitarbeiter möchte ich meine registrierten Passkeys einsehen und einzeln löschen können, damit ich die Kontrolle über meine Zugangsmittel behalte, z.B. bei Geräteverlust.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Passkey-Registrierung – Challenge-Erzeugung
Das System SHALL für einen eingeloggten Nutzer über `POST /api/auth/passkeys/register/options` WebAuthn-Registrierungsoptionen (inkl. kryptographisch zufälliger Challenge, Nutzer-Informationen, Relying-Party-Angaben) gemäß WebAuthn-Spezifikation erzeugen.

##### Scenario: Registrierungsoptionen werden erzeugt
- **GIVEN** ein eingeloggter Mitarbeiter
- **WHEN** `POST /api/auth/passkeys/register/options` aufgerufen wird
- **THEN** antwortet der Service mit 200 OK und liefert gültige `PublicKeyCredentialCreationOptions` (inkl. Challenge, die serverseitig für den anschließenden Abschluss vorgehalten wird)

#### Requirement: Passkey-Registrierung – Abschluss
Das System SHALL über `POST /api/auth/passkeys/register` die vom Authenticator erzeugte Attestation Response gegen die zuvor ausgegebene Challenge validieren und bei Erfolg einen neuen `PasskeyCredential` für den Nutzer persistieren.

##### Scenario: Erfolgreiche Registrierung
- **GIVEN** zuvor ausgegebene, noch gültige Registrierungs-Challenge für den eingeloggten Nutzer
- **WHEN** `POST /api/auth/passkeys/register` mit einer zur Challenge passenden, gültigen Attestation Response aufgerufen wird
- **THEN** antwortet der Service mit 201 Created, der neue Passkey ist in der Liste der Passkeys des Nutzers sichtbar

##### Scenario: Registrierung mit ungültiger Attestation
- **GIVEN** eine gültige Registrierungs-Challenge
- **WHEN** `POST /api/auth/passkeys/register` mit einer manipulierten oder nicht zur Challenge passenden Attestation Response aufgerufen wird
- **THEN** antwortet der Service mit 400 Bad Request, kein Passkey wird gespeichert

#### Requirement: Passkey-Login – Challenge-Erzeugung
Das System SHALL über `POST /api/auth/passkeys/login/options` Login-Optionen (Assertion Challenge) erzeugen; der Endpoint ist anonym erreichbar, da der Nutzer zu diesem Zeitpunkt noch nicht eingeloggt ist.

##### Scenario: Login-Optionen werden erzeugt
- **GIVEN** eine E-Mail-Adresse eines Nutzers mit mindestens einem registrierten Passkey
- **WHEN** `POST /api/auth/passkeys/login/options` mit dieser E-Mail aufgerufen wird
- **THEN** antwortet der Service mit 200 OK und liefert `PublicKeyCredentialRequestOptions` mit den registrierten Credential-Ids des Nutzers als `allowCredentials`

##### Scenario: Login-Optionen für Nutzer ohne Passkey
- **GIVEN** eine E-Mail-Adresse eines Nutzers ohne registrierten Passkey (oder eine unbekannte E-Mail)
- **WHEN** `POST /api/auth/passkeys/login/options` aufgerufen wird
- **THEN** antwortet der Service mit 200 OK und leeren `allowCredentials` (keine User-Enumeration über Statuscode-Unterschiede, analog zum bestehenden Login-Verhalten)

#### Requirement: Passkey-Login – Abschluss
Das System SHALL über `POST /api/auth/passkeys/login` die Assertion Response gegen die ausgegebene Challenge und den gespeicherten Public Key validieren, den `SignCount` prüfen/aktualisieren und bei Erfolg ein vollständiges JWT ausstellen (identisches Response-Schema wie der bestehende Passwort-Login).

##### Scenario: Erfolgreicher Passkey-Login
- **GIVEN** eine gültige Login-Challenge und ein zuvor registrierter Passkey des Nutzers
- **WHEN** `POST /api/auth/passkeys/login` mit einer gültigen, zur Challenge passenden Assertion Response aufgerufen wird
- **THEN** antwortet der Service mit 200 OK und liefert ein JWT mit den gleichen Claims wie beim bestehenden Passwort-Login

##### Scenario: Login mit ungültiger Assertion
- **GIVEN** eine gültige Login-Challenge
- **WHEN** `POST /api/auth/passkeys/login` mit einer nicht passenden oder manipulierten Assertion Response aufgerufen wird
- **THEN** antwortet der Service mit 401 Unauthorized, kein Token wird ausgestellt

##### Scenario: SignCount-Anomalie (Clone-Erkennung)
- **GIVEN** ein gespeicherter Passkey mit bekanntem `SignCount`
- **WHEN** eine Assertion Response mit einem `SignCount` kleiner oder gleich dem gespeicherten Wert eintrifft
- **THEN** antwortet der Service mit 401 Unauthorized und markiert den Vorgang als potenziellen Klon-Verdacht (Basis für künftiges Audit-Logging gemäß `identity-audit-log.openspec.md`)

#### Requirement: Passkeys verwalten (auflisten/löschen)
Das System SHALL dem eingeloggten Nutzer über `GET /api/auth/passkeys` eine Liste seiner eigenen registrierten Passkeys (ohne Public-Key-Rohdaten) und über `DELETE /api/auth/passkeys/{id}` das Löschen eines eigenen Passkeys ermöglichen.

##### Scenario: Eigene Passkeys auflisten
- **GIVEN** ein Nutzer mit zwei registrierten Passkeys
- **WHEN** `GET /api/auth/passkeys` aufgerufen wird
- **THEN** antwortet der Service mit 200 OK und liefert beide Passkeys mit Name, Erstellungsdatum und Zeitpunkt der letzten Nutzung, aber ohne den Public Key im Klartext

##### Scenario: Eigenen Passkey löschen
- **GIVEN** ein Nutzer mit einem registrierten Passkey mit bekannter Id
- **WHEN** `DELETE /api/auth/passkeys/{id}` mit dieser Id aufgerufen wird
- **THEN** antwortet der Service mit 204 No Content, der Passkey ist danach nicht mehr für den Login nutzbar

##### Scenario: Löschen eines fremden Passkeys
- **GIVEN** ein Nutzer A und ein Passkey, der Nutzer B gehört
- **WHEN** Nutzer A `DELETE /api/auth/passkeys/{id}` mit der Id des Passkeys von Nutzer B aufruft
- **THEN** antwortet der Service mit 404 Not Found (keine Bestätigung der Existenz fremder Ressourcen)

## 4. UI/UX Requirements
Frontend-Umsetzung erfolgt in separater Session, hier nur Backend-Vertrag/API. Hinweis: Browser-seitige WebAuthn-API-Aufrufe (`navigator.credentials.create/get`) sind Frontend-Sache und nicht Teil dieser Spec.

## 5. API & Backend Requirements
- **Authentifizierung/Autorisierung**: `POST /api/auth/passkeys/register/options`, `POST /api/auth/passkeys/register`, `GET /api/auth/passkeys`, `DELETE /api/auth/passkeys/{id}` erfordern gültiges JWT (`[Authorize]`, Self-Service, kein Rollenerfordernis). `POST /api/auth/passkeys/login/options` und `POST /api/auth/passkeys/login` sind `[AllowAnonymous]` (wie bestehender Passwort-Login).
- **Endpoints**:
  - `POST /api/auth/passkeys/register/options`: Request leer (Identität aus JWT) → `PublicKeyCredentialCreationOptions` (Fido2NetLib-Standardformat).
  - `POST /api/auth/passkeys/register`: `{ attestationResponse, friendlyName? }` → 201 Created `{ id, friendlyName, createdAtUtc }`.
  - `POST /api/auth/passkeys/login/options`: `{ email }` → `PublicKeyCredentialRequestOptions`.
  - `POST /api/auth/passkeys/login`: `{ assertionResponse }` → `{ accessToken, expiresAtUtc, userId, role, displayName }` (identisch zum bestehenden Login-Erfolgs-Response).
  - `GET /api/auth/passkeys`: → `[{ id, friendlyName, createdAtUtc, lastUsedAtUtc }]`.
  - `DELETE /api/auth/passkeys/{id}`: → 204 No Content / 404 Not Found.
- **Modelle/Entitäten (PostgreSQL, `identity_db`)**:
  - `PasskeyCredential`: `Id` (Guid), `UserId` (Guid, FK), `CredentialId` (byte[], unique, Base64Url-codiert extern), `PublicKey` (byte[]), `SignCount` (uint), `FriendlyName` (string, nutzerdefiniert oder Default), `AaGuid` (Guid, Authenticator-Modell), `CreatedAtUtc` (DateTime), `LastUsedAtUtc` (DateTime?).
  - Temporäre Challenge-Speicherung: `PasskeyChallenge` (kurzlebig, z.B. In-Memory-Cache mit TTL statt eigener Tabelle, da nur für die Dauer der Ceremony relevant) — Designentscheidung, siehe Abschnitt 7.

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Nein.
- **Prompt/System Message**: Nicht relevant.

## 7. Design (Technischer Ansatz)
- **Bibliothek:** **Fido2NetLib** wird für die gesamte WebAuthn-Server-Logik (Options-Generierung, Attestation-/Assertion-Validierung) genutzt; keine Eigenimplementierung des WebAuthn/CBOR/COSE-Protokolls.
- **Relying Party Konfiguration:** RP-Id und erlaubte Origins werden über Konfiguration (`appsettings.json`/Environment Variables) je Umgebung gesetzt, nicht hardcoded (siehe `CLAUDE.md`, Abschnitt Sicherheit).
- **Challenge-Speicherung:** Registrierungs- und Login-Challenges werden kurzlebig (TTL wenige Minuten) serverseitig zwischengespeichert (z.B. `IDistributedCache`/In-Memory-Cache, Schlüssel = Session-/Request-Kennung), nicht in `identity_db` persistiert, da sie nach Ceremony-Abschluss wertlos sind — vermeidet unnötige DB-Last und Clean-up-Jobs.
- **SignCount-Prüfung:** Nach jeder erfolgreichen Assertion wird `SignCount` aktualisiert; ein nicht-steigender Wert gilt als Hinweis auf eine geklonte Authenticator-Credential und führt zur Ablehnung (siehe Scenario oben). Konkrete Reaktion (z.B. automatische Sperrung aller Passkeys des Nutzers) ist bewusst nicht Teil dieser ersten Iteration.
- **Voraussetzung:** Baut auf der in `identity-mfa-totp.openspec.md` Abschnitt 7 beschriebenen `identity_db`-Infrastruktur (EF Core, Migrations-Hosted-Service) auf; diese Change fügt lediglich eine weitere Migration/Tabelle hinzu.
- **Bewusste Vereinfachung:** Kein Attestation-Trust-Chain-Checking gegen FIDO-Metadata-Service (MDS) in dieser Iteration — es wird nur "None"/"Self"-Attestation akzeptiert, was für ein internes Business-CRM (kein hochregulierter Kontext) ausreichend ist; strengere Attestation-Policies sind ein möglicher Folge-Change.
- **Bewusste Vereinfachung:** Kein Rückfall auf Passkey als alleinigen Faktor ohne Möglichkeit, das Konto bei Verlust aller Passkeys wiederherzustellen, außer über den bestehenden Passwort-Login — es wird kein separates Backup-Verfahren eingeführt.

## 8. Tasks (Implementierungs-Checkliste)
- [ ] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/IdentityService.Tests`
- [ ] 2. `PasskeyCredential`-Entität und EF-Core-Migration gegen bestehende `identity_db` implementieren
- [ ] 3. Fido2NetLib-Integration (Registrierung/Login Options + Abschluss) in Application/Infrastructure implementieren
- [ ] 4. Challenge-Caching-Mechanismus implementieren
- [ ] 5. Verwaltungs-Endpunkte (Liste/Löschen) implementieren inkl. Ownership-Check (404 statt 403 bei fremden Ids)
- [ ] 6. SignCount-Validierung und Ablehnung bei Anomalie implementieren
- [ ] 7. Doku/OpenAPI aktualisiert

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün.
- [ ] Backend API (C# 14) ist implementiert und getestet (TUnit) in `Tests/IdentityService.Tests`.
- [ ] Fido2NetLib korrekt gegen konfigurierbare RP-Id/Origin verdrahtet, keine hardcoded Werte.
- [ ] Ownership-Checks (Scenario "fremder Passkey") durch Review bestätigt (`sec-agent`).
- [ ] Swagger/OpenAPI Compliance ist gegeben.
- [ ] Spec ist archiviert (Status `archived`, Requirements in `UserStories/specs/identity.openspec.md` übernommen).
