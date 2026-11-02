# Change: Sicherheits-Audit-Log

> **Status:** proposed
> Ablage: `UserStories/identity-audit-log.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-identity-audit-log.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Mit MFA, Passkeys und Device-Trust (`identity-mfa-totp.openspec.md`, `identity-passkeys-webauthn.openspec.md`, `identity-device-trust.openspec.md`) entstehen sicherheitsrelevante Ereignisse (Login-Erfolg/-Fehlschlag, Rollenänderungen, Passwortänderungen, Geräte-Freigabe/-Sperrung, MFA-/Passkey-Verwaltung), die aktuell nirgends nachvollziehbar protokolliert werden. Ohne zentrales, manipulationssicheres (append-only) Audit-Log kann im Sicherheitsvorfall nicht rekonstruiert werden, was passiert ist — ein Compliance- und Incident-Response-Risiko.
- **What Changes:**
  - Neue append-only Tabelle `AuditLogEntry` im `identity-service`.
  - Neues Application-Interface `IAuditLogger`, das von dieser Change zuerst bereitgestellt wird, damit `identity-mfa-totp.openspec.md`, `identity-passkeys-webauthn.openspec.md` und `identity-device-trust.openspec.md` Audit-Events darüber schreiben können. **Wichtiger Hinweis zur Umsetzungsreihenfolge:** Da die drei anderen Changes dieses Interface referenzieren, SHALL `IAuditLogger` (inkl. `AuditLogEntry`-Entität und No-Op-/Persistenz-Implementierung) als gemeinsame Grundlage zuerst implementiert werden, bevor oder parallel zu den Audit-Aufrufen in den anderen drei Changes; andernfalls müssen jene Changes vorübergehend gegen ein Platzhalter-Interface entwickelt werden.
  - Admin-Einsicht in das Audit-Log (paginiert, filterbar) sowie CSV-Export.
- **Impact:** Betrifft die Capability-Spec `UserStories/specs/identity.openspec.md` (wird nach Abschluss/Archivierung aktualisiert, nicht jetzt). Layer: Domain (`AuditLogEntry`, `AuditEventType`), Application (`IAuditLogger`-Interface, Query-Use-Cases für Abfrage/Export), Infrastructure (EF-Core-Implementierung gegen `identity_db`, append-only Durchsetzung), Presentation (`identity-service` Admin-Endpunkte). Setzt die in `identity-mfa-totp.openspec.md` Abschnitt 7 eingeführte `identity_db`-Infrastruktur voraus. Wird von `identity-mfa-totp.openspec.md`, `identity-passkeys-webauthn.openspec.md` und `identity-device-trust.openspec.md` als Abhängigkeit referenziert (deren konkrete `IAuditLogger`-Aufrufe sind nicht Teil dieser Spec, sondern der jeweiligen Spec selbst — hier wird nur die Grundlage/Interface spezifiziert und die hier eigenständig erfassbaren Ereignistypen LoginSucceeded/LoginFailed).

## 2. User Stories
- Als Admin möchte ich ein lückenloses, nicht nachträglich veränderbares Protokoll sicherheitsrelevanter Ereignisse einsehen können, damit ich im Verdachtsfall nachvollziehen kann, wer was wann getan hat.
- Als Admin möchte ich das Audit-Log nach Ereignistyp, Zeitraum und Nutzer filtern können, damit ich gezielt nach verdächtigen Mustern suchen kann.
- Als Admin möchte ich das gefilterte Audit-Log als CSV exportieren können, damit ich es für externe Analyse oder Aufbewahrungspflichten weiterverarbeiten kann.
- Als Entwickler (Backend-Agent) möchte ich ein einfaches `IAuditLogger`-Interface nutzen können, damit ich sicherheitsrelevante Ereignisse aus anderen Use Cases heraus protokollieren kann, ohne die Audit-Infrastruktur selbst implementieren zu müssen.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Append-only Audit-Log-Persistenz
Das System SHALL sicherheitsrelevante Ereignisse als unveränderliche `AuditLogEntry`-Datensätze persistieren; es SHALL keinen Applikations-Endpoint oder Use Case geben, der einen bestehenden `AuditLogEntry` aktualisiert oder löscht.

##### Scenario: Ereignis wird protokolliert
- **GIVEN** ein über `IAuditLogger` ausgelöstes Ereignis (z.B. `LoginSucceeded`) mit Zeitstempel, Akteur-UserId, Event-Typ und Details
- **WHEN** das Ereignis protokolliert wird
- **THEN** wird ein neuer `AuditLogEntry`-Datensatz persistiert, der über die öffentliche API nicht mehr verändert oder gelöscht werden kann

#### Requirement: Erfassung von Login-Ereignissen
Das System SHALL erfolgreiche und fehlgeschlagene Login-Versuche als `AuditLogEntry` mit Event-Typ `LoginSucceeded` bzw. `LoginFailed` inkl. IP-Adresse protokollieren.

##### Scenario: Erfolgreicher Login wird protokolliert
- **GIVEN** ein Nutzer mit korrekten Anmeldedaten
- **WHEN** der Login über `POST /api/auth/login` (ggf. inkl. MFA-/Geräte-Bestätigung) vollständig abgeschlossen wird
- **THEN** wird ein `AuditLogEntry` mit Event-Typ `LoginSucceeded`, der betroffenen UserId und der Client-IP persistiert

##### Scenario: Fehlgeschlagener Login wird protokolliert
- **GIVEN** ein Login-Versuch mit falschem Passwort
- **WHEN** `POST /api/auth/login` entsprechend fehlschlägt
- **THEN** wird ein `AuditLogEntry` mit Event-Typ `LoginFailed`, der betroffenen UserId (sofern die E-Mail einem Nutzer zuordenbar war) und der Client-IP persistiert

#### Requirement: Admin-Einsicht in das Audit-Log (paginiert, filterbar)
Das System SHALL Admins über `GET /api/audit-log` eine paginierte, nach Event-Typ, Zeitraum und betroffenem Nutzer filterbare Abfrage des Audit-Logs ermöglichen.

##### Scenario: Admin ruft Audit-Log ohne Filter ab
- **GIVEN** mehrere vorhandene `AuditLogEntry`-Datensätze
- **WHEN** ein Admin `GET /api/audit-log?page=1&pageSize=50` aufruft
- **THEN** antwortet der Service mit 200 OK und liefert die neuesten Einträge absteigend nach Zeitstempel sortiert, paginiert gemäß Parametern

##### Scenario: Admin filtert nach Event-Typ und Zeitraum
- **GIVEN** vorhandene Einträge verschiedener Event-Typen über mehrere Tage
- **WHEN** ein Admin `GET /api/audit-log?eventType=LoginFailed&fromUtc=...&toUtc=...` aufruft
- **THEN** antwortet der Service mit 200 OK und liefert ausschließlich Einträge mit Event-Typ `LoginFailed` innerhalb des angegebenen Zeitraums

##### Scenario: Nicht-Admin versucht Audit-Log abzurufen
- **GIVEN** ein eingeloggter Mitarbeiter ohne Admin-Rolle
- **WHEN** `GET /api/audit-log` aufgerufen wird
- **THEN** antwortet der Service mit 403 Forbidden

#### Requirement: CSV-Export des Audit-Logs
Das System SHALL Admins über `GET /api/audit-log/export` einen CSV-Export des (mit denselben Parametern wie bei der Abfrage) gefilterten Audit-Logs ermöglichen.

##### Scenario: Export mit aktiven Filtern
- **GIVEN** ein Admin mit gültigem Token und aktiven Filterparametern (z.B. Zeitraum)
- **WHEN** `GET /api/audit-log/export?fromUtc=...&toUtc=...` aufgerufen wird
- **THEN** antwortet der Service mit 200 OK, `Content-Type: text/csv`, und liefert genau die Einträge, die auch `GET /api/audit-log` mit identischen Filtern zurückgeben würde

##### Scenario: Export durch Nicht-Admin
- **GIVEN** ein eingeloggter Mitarbeiter ohne Admin-Rolle
- **WHEN** `GET /api/audit-log/export` aufgerufen wird
- **THEN** antwortet der Service mit 403 Forbidden

#### Requirement: `IAuditLogger`-Application-Interface als gemeinsame Grundlage
Das System SHALL ein Application-Interface `IAuditLogger` mit einer Methode zum Schreiben eines Audit-Ereignisses (Event-Typ, Akteur-UserId, optionale abweichende betroffene UserId, Details, IP-Adresse) bereitstellen, das von anderen Use Cases im `identity-service` (MFA, Passkeys, Device Trust) referenziert werden kann.

##### Scenario: Anderer Use Case schreibt ein Ereignis
- **GIVEN** ein Use Case außerhalb dieser Spec (z.B. MFA-Aktivierung aus `identity-mfa-totp.openspec.md`), der `IAuditLogger` injiziert bekommt
- **WHEN** dieser Use Case `IAuditLogger.LogAsync(...)` mit Event-Typ `MfaEnabled` aufruft
- **THEN** wird ein entsprechender `AuditLogEntry` persistiert, ohne dass der aufrufende Use Case Kenntnis von der Persistenzimplementierung haben muss

## 4. UI/UX Requirements
Frontend-Umsetzung erfolgt in separater Session, hier nur Backend-Vertrag/API.

## 5. API & Backend Requirements
- **Authentifizierung/Autorisierung**: `GET /api/audit-log` und `GET /api/audit-log/export` erfordern Rolle `Admin` (`[Authorize(Roles = "Admin")]`). `IAuditLogger` ist ein internes Application-Interface ohne eigenen HTTP-Endpoint.
- **Endpoints**:
  - `GET /api/audit-log`: Query-Parameter `page`, `pageSize`, `eventType?`, `userId?`, `fromUtc?`, `toUtc?` → `{ items: [{ id, timestampUtc, eventType, actorUserId, affectedUserId?, details, ipAddress }], totalCount, page, pageSize }`.
  - `GET /api/audit-log/export`: gleiche Filterparameter wie oben → CSV-Datei (gleiche Spalten wie `items`).
- **Modelle/Entitäten (PostgreSQL, `identity_db`)**:
  - `AuditLogEntry`: `Id` (Guid), `TimestampUtc` (DateTime, Default `UtcNow`, nicht änderbar), `EventType` (string/Enum, siehe `AuditEventType`), `ActorUserId` (Guid), `AffectedUserId` (Guid?, nur gesetzt wenn abweichend vom Akteur), `Details` (string, JSON oder Freitext, keine Secrets/Klartext-Credentials), `IpAddress` (string).
  - `AuditEventType` (Enum/Konstanten): `LoginSucceeded`, `LoginFailed`, `RoleChanged`, `PasswordChanged`, `DeviceApproved`, `DeviceRevoked`, `MfaEnabled`, `MfaDisabled`, `PasskeyRegistered`, `PasskeyRemoved`; `ApiKeyRotated` ist **optional** und wird nur erfasst, falls im Projekt ein API-Key-Rotationsmechanismus existiert (aktuell nicht vorhanden — Platzhalter für zukünftige Nutzung, kein aktiver Schreibpfad in dieser Iteration).

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Nein.
- **Prompt/System Message**: Nicht relevant.

## 7. Design (Technischer Ansatz)
- **`IAuditLogger`-Interface (Application-Layer von `identity-service`):**
  ```csharp
  public interface IAuditLogger
  {
      Task LogAsync(
          AuditEventType eventType,
          Guid actorUserId,
          Guid? affectedUserId,
          string details,
          string ipAddress,
          CancellationToken ct = default);
  }
  ```
  Implementierung (Infrastructure-Layer) schreibt direkt und synchron (innerhalb derselben DB-Transaktion wie die auslösende Aktion, wo sinnvoll, z.B. Login) einen `AuditLogEntry` gegen `identity_db`. Kein Messaging/Event-Bus notwendig, da Audit-Log und auslösende Aktion im selben Service/derselben DB liegen (kein Cross-Service-Szenario wie in `docs/adr/0001-microservices-architektur-crm.md`).
- **Append-only-Durchsetzung:** Auf Anwendungsebene gibt es schlicht keine Update/Delete-Methode im Repository für `AuditLogEntry`; zusätzlich auf DB-Ebene optional ein PostgreSQL-`REVOKE UPDATE, DELETE`-Grant für die Anwendungsrolle als Härtung (Empfehlung, kein Hard-Requirement dieser Iteration, da das Service-Konto ohnehin nur über die EF-Core-Repository-Schicht zugreift).
- **Reihenfolge/Abhängigkeit:** Da `identity-mfa-totp.openspec.md`, `identity-passkeys-webauthn.openspec.md` und `identity-device-trust.openspec.md` `IAuditLogger` aufrufen sollen, wird empfohlen, diese Change (zumindest Interface + Entität + Basis-Implementierung) zuerst oder parallel-vorlaufend umzusetzen. Die konkreten `LogAsync`-Aufrufe für `MfaEnabled`/`MfaDisabled`, `PasskeyRegistered`/`PasskeyRemoved`, `DeviceApproved`/`DeviceRevoked`, `RoleChanged`, `PasswordChanged` werden in den jeweiligen Changes selbst ergänzt (dort als Folgeaufgabe zu vermerken), nicht hier — diese Spec deckt nur `LoginSucceeded`/`LoginFailed` als unmittelbar im `identity-service` selbst entstehende Ereignisse ab sowie die Bereitstellung des Interfaces.
- **Voraussetzung:** Baut auf der in `identity-mfa-totp.openspec.md` Abschnitt 7 eingeführten `identity_db`-Infrastruktur auf.
- **Bewusste Vereinfachung:** Kein kryptographisches Hash-Chaining/Merkle-Log zur Manipulationssicherheit (wie es z.B. für regulatorisch strenge Audit-Trails nötig wäre) — Append-only auf Anwendungs-/DB-Rollenebene wird für dieses interne CRM als ausreichend bewertet. Härtung per DB-Grants ist eine optionale Folgeaufgabe.
- **Bewusste Vereinfachung:** `Details` wird als JSON-/Freitext-String ohne festes Schema je Event-Typ gespeichert; eine stärker typisierte Payload je Event-Typ ist ein möglicher Folge-Change, falls Abfrage/Analyse das erfordert.

## 8. Tasks (Implementierungs-Checkliste)
- [ ] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/IdentityService.Tests`
- [ ] 2. `AuditLogEntry`-Entität, `AuditEventType`-Enum und EF-Core-Migration gegen `identity_db` implementieren
- [ ] 3. `IAuditLogger`-Interface (Application) und EF-Core-basierte Implementierung (Infrastructure) implementieren
- [ ] 4. `LoginSucceeded`/`LoginFailed`-Protokollierung im bestehenden Login-Use-Case verdrahten
- [ ] 5. `GET /api/audit-log` (paginiert, filterbar) implementieren
- [ ] 6. `GET /api/audit-log/export` (CSV) implementieren
- [ ] 7. Doku/OpenAPI aktualisiert, Hinweis für andere Changes zur Nutzung von `IAuditLogger` in `UserStories/specs/identity.openspec.md` ergänzen

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün.
- [ ] Backend API (C# 14) ist implementiert und getestet (TUnit) in `Tests/IdentityService.Tests`.
- [ ] Kein Update/Delete-Pfad für `AuditLogEntry` im Code vorhanden (Review durch `architect-agent`/`sec-agent`).
- [ ] `IAuditLogger` ist für die drei abhängigen Changes nutzbar (Interface stabil, dokumentiert).
- [ ] Swagger/OpenAPI Compliance ist gegeben.
- [ ] Spec ist archiviert (Status `archived`, Requirements in `UserStories/specs/identity.openspec.md` übernommen).
