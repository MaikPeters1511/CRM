# Change: Mitarbeiterverwaltung

> **Status:** in-progress
> Ablage: `UserStories/mitarbeiterverwaltung.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-mitarbeiterverwaltung.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Für die Terminvergabe müssen Mitarbeiter mit ihren Rollen, Qualifikationen und Verfügbarkeiten (Arbeitszeiten, Abwesenheiten) im System abgebildet sein. Ohne Mitarbeiterverwaltung kann kein Termin einem verfügbaren Mitarbeiter zugeordnet werden.
- **What Changes:**
  - Neuer eigenständiger Microservice `employee-service` (Bounded Context "Employee Management").
  - Anlegen/Bearbeiten/Deaktivieren von Mitarbeitern inkl. Rollen (RBAC-Grundlage) und Qualifikationen/Leistungen.
  - Verwaltung von Arbeitszeiten und Abwesenheiten (Urlaub, Krankheit) als Grundlage für die Verfügbarkeitsprüfung im `appointment-service`.
- **Impact:** Neuer Microservice `employee-service` mit eigener PostgreSQL-Datenbank `employee_db`. Wird vom `appointment-service` (Change `terminverwaltung.openspec.md`) zur Verfügbarkeitsprüfung referenziert (Mitarbeiter ⇄ Termin). Rollen-/Claims-Modell ist mit `sec-agent` (Microsoft Identity) abzustimmen. Servicegrenzen und Kommunikationsmuster sind festgelegt in `docs/adr/0001-microservices-architektur-crm.md`. **Hinweis:** Die in Abschnitt 4 genannten Frontend-Komponenten werden gemäß `frontend-konsolidierung.openspec.md` aus dem eigenständigen `employee-web` in die gemeinsame Anwendung `crm-web` migriert; die Requirements/Scenarios in Abschnitt 3 bleiben davon unberührt.

## 2. User Stories
- Als Admin möchte ich einen neuen Mitarbeiter mit Stammdaten, Rolle und Leistungen anlegen, damit er Terminen zugeordnet werden kann.
- Als Admin möchte ich einem Mitarbeiter Arbeitszeiten (Wochenplan) hinterlegen, damit nur innerhalb dieser Zeiten Termine vergeben werden können.
- Als Mitarbeiter möchte ich eigene Abwesenheiten (Urlaub/Krankheit) eintragen, damit in diesen Zeiträumen keine Termine gebucht werden.
- Als Admin möchte ich Mitarbeiter nach Rolle/Leistung filtern, damit ich für eine bestimmte Dienstleistung passende Mitarbeiter finde.
- Als Admin möchte ich einen Mitarbeiter deaktivieren (Soft-Delete), damit ausgeschiedene Mitarbeiter nicht mehr für neue Termine verfügbar sind, die Historie aber erhalten bleibt.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Mitarbeiter anlegen
Das System SHALL das Anlegen eines Mitarbeiters mit Pflichtfeldern (Vorname, Nachname, E-Mail, Rolle) ermöglichen.

##### Scenario: Mitarbeiter mit gültigen Pflichtdaten anlegen
- **GIVEN** ein Admin ist authentifiziert und befindet sich im Mitarbeitermodul
- **WHEN** er Pflichtfelder inkl. Rolle gültig ausfüllt und speichert
- **THEN** wird ein neuer Mitarbeiterdatensatz mit eindeutiger Id und zugewiesener Rolle persistiert

##### Scenario: Mitarbeiter mit bereits vergebener E-Mail anlegen
- **GIVEN** ein Mitarbeiter mit der E-Mail `max@example.com` existiert bereits
- **WHEN** ein Admin einen neuen Mitarbeiter mit derselben E-Mail anlegt
- **THEN** wird ein Konfliktfehler angezeigt und kein Datensatz persistiert

#### Requirement: Leistungen/Qualifikationen zuordnen
Das System SHALL die Zuordnung von einer oder mehreren Leistungen (Services) zu einem Mitarbeiter ermöglichen.

##### Scenario: Leistung zuordnen
- **GIVEN** ein bestehender Mitarbeiter und eine definierte Leistung "Beratung" existieren
- **WHEN** ein Admin die Leistung dem Mitarbeiter zuordnet und speichert
- **THEN** erscheint der Mitarbeiter bei der Terminvergabe als möglicher Ansprechpartner für "Beratung"

#### Requirement: Arbeitszeiten verwalten
Das System SHALL die Definition wiederkehrender Wochenarbeitszeiten je Mitarbeiter ermöglichen.

##### Scenario: Wochenarbeitszeit hinterlegen
- **GIVEN** ein Mitarbeiter ohne definierte Arbeitszeiten
- **WHEN** ein Admin Mo–Fr 09:00–17:00 Uhr hinterlegt und speichert
- **THEN** wird dieser Zeitraum als verfügbares Zeitfenster für die Terminvergabe berücksichtigt

#### Requirement: Abwesenheiten erfassen
Das System SHALL das Erfassen von Abwesenheiten (Urlaub, Krankheit, Sonstige) mit Zeitraum ermöglichen.

##### Scenario: Urlaub eintragen
- **GIVEN** ein Mitarbeiter hat für einen Zeitraum bereits gebuchte Termine außerhalb des geplanten Urlaubs
- **WHEN** er einen Urlaub für einen termin-freien Zeitraum einträgt
- **THEN** wird die Abwesenheit gespeichert und der Mitarbeiter für diesen Zeitraum als nicht verfügbar markiert

##### Scenario: Urlaub kollidiert mit bestehendem Termin
- **GIVEN** ein Mitarbeiter hat einen bestätigten Termin am 15.10.
- **WHEN** er versucht, für den 15.10. eine Abwesenheit einzutragen
- **THEN** wird eine Konfliktwarnung angezeigt und die Bestätigung erfordert eine explizite Bestätigung oder Terminverschiebung (Verweis auf `appointment-service`)

#### Requirement: Mitarbeiter deaktivieren (Soft-Delete)
Das System SHALL das Deaktivieren eines Mitarbeiters ermöglichen, ohne bestehende Termine oder Historie zu löschen.

##### Scenario: Mitarbeiter deaktivieren
- **GIVEN** ein Admin betrachtet einen aktiven Mitarbeiter ohne zukünftige offene Termine
- **WHEN** er "Deaktivieren" auswählt und bestätigt
- **THEN** wird der Mitarbeiter als `IsActive = false` markiert und erscheint nicht mehr in der Verfügbarkeitsauswahl für neue Termine

## 4. UI/UX Requirements
- **Komponenten**: `employee-list.component.ts` (DaisyUI `table`, Filter nach Rolle/Leistung), `employee-form.component.ts` (Stammdaten, Rollen-Multiselect, Leistungs-Zuordnung), `employee-schedule.component.ts` (Wochenplan-Editor, DaisyUI `tabs`/`calendar`-ähnliche Zeitraster), `absence-form.component.ts` (Abwesenheiten mit Datepicker und Konfliktwarnung).
- **State**: Angular Signals/State-Service für Mitarbeiterliste, ausgewählten Mitarbeiter, Arbeitszeiten- und Abwesenheits-State inkl. Konfliktprüfung mit `appointment-service`.
- **i18n / a11y**: Alle Labels/Fehlermeldungen über DE/EN i18n-Keys (`employee.*`); Zeitraster-Editor vollständig per Tastatur bedienbar (Pfeiltasten zur Navigation zwischen Zeitslots); Konfliktwarnungen als `role="alert"`; Formulare mit `aria-invalid`/`aria-describedby`.

## 5. API & Backend Requirements
- **Service:** `employee-service` (eigenständig deploybar, eigene PostgreSQL-Instanz `employee_db`, eigenes Aspire-Ressourcen-Modul).
- **Authentifizierung/Autorisierung**: `[Authorize]` Standard; Rolle `Admin` für Anlegen/Bearbeiten/Deaktivieren von Mitarbeitern und Rollenzuweisung; Mitarbeiter selbst dürfen nur eigene Abwesenheiten anlegen (`Employee`-Rolle, Ownership-Check über Microsoft Identity Claims/UserId).
- **Endpoints**:
  - `GET /api/employees?search=&role=&serviceId=&page=`: Paginierte, filterbare Mitarbeiterliste
  - `GET /api/employees/{id}`: Mitarbeiterdetails inkl. Leistungen, Arbeitszeiten, Abwesenheiten
  - `POST /api/employees`: Mitarbeiter anlegen
  - `PUT /api/employees/{id}`: Mitarbeiter aktualisieren
  - `POST /api/employees/{id}/deactivate`: Mitarbeiter deaktivieren (Rolle `Admin`) → publiziert Integration Event `EmployeeDeactivated`
  - `PUT /api/employees/{id}/schedule`: Wochenarbeitszeiten setzen
  - `POST /api/employees/{id}/absences`: Abwesenheit anlegen
  - `GET /api/employees/{id}/availability?from=&to=`: Verfügbare Zeitfenster (kombiniert Arbeitszeit − Abwesenheit − bestehende Termine via Call an `appointment-service`)
- **Modelle/Entitäten (PostgreSQL, eigene DB `employee_db`):**
  - `Employee`: `Id (Guid)`, `FirstName (string)`, `LastName (string)`, `Email (string)`, `Role (enum: Admin, Employee)`, `IsActive (bool)`, `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`
  - `EmployeeService` (Zuordnungstabelle): `EmployeeId (Guid)`, `ServiceId (Guid)`, `ServiceName (string)`
  - `WorkingHours`: `Id (Guid)`, `EmployeeId (Guid)`, `Weekday (enum)`, `StartTime`, `EndTime`
  - `Absence`: `Id (Guid)`, `EmployeeId (Guid)`, `Type (enum: Vacation, Sick, Other)`, `From (DateOnly)`, `To (DateOnly)`
- **Inter-Service-Kommunikation:** `employee-service` fragt bei Abwesenheits-/Arbeitszeitkonflikten bestehende Termine synchron beim `appointment-service` ab; `appointment-service` fragt umgekehrt Verfügbarkeit über `GET /api/employees/{id}/availability` ab. Kommunikationsmuster final gemäß `docs/adr/0001-microservices-architektur-crm.md`.

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Nein.
- **Prompt/System Message**: Entfällt.

## 7. Design (Technischer Ansatz)
`employee-service` als eigenständiger Microservice mit interner Clean-Architecture-Schichtung: Domain-Layer: `Employee` als Aggregate Root mit `WorkingHours`/`Absence` als zugehörige Entities/Value Objects, Domain Event `EmployeeDeactivated`. Application-Layer: CQRS Commands (`CreateEmployeeCommand`, `UpdateEmployeeCommand`, `DeactivateEmployeeCommand`, `SetWorkingHoursCommand`, `AddAbsenceCommand`) und Queries (`SearchEmployeesQuery`, `GetEmployeeAvailabilityQuery`) via Wolverine. Infrastructure: EF Core Repository gegen `employee_db`. Verfügbarkeitsberechnung kombiniert lokale Arbeitszeiten/Abwesenheiten mit Terminstatus aus `appointment-service` (synchroner Call, ggf. mit Caching/Circuit-Breaker via Polly). Service wird über .NET Aspire AppHost registriert. Die endgültige Entscheidung zu Servicegrenzen und Kommunikationsmustern (sync REST vs. Event-Choreografie) für `customer-service`, `employee-service` und `appointment-service` ist dokumentiert in `docs/adr/0001-microservices-architektur-crm.md`.

## 8. Tasks (Implementierungs-Checkliste)
- [x] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/`
- [x] 2. Backend implementieren (Domain → Application → Infrastructure → Web)
- [x] 3. Frontend implementieren (Angular 22, DaisyUI 5, i18n, ARIA)
- [x] 4. E2E-Tests (QA) geschrieben UND vollstaendig gruen verifiziert (qa-agent, finaler Lauf 2026-10-01 nach Bug-8-Fix, erneut bestaetigt 2026-10-01 im finalen Gesamt-Verifikationslauf nach den Bug-9/10-Fixes — Gesamtsuite 30/30 gruen) — `Tests/E2E/tests/employees.spec.ts` (7 Tests). Real gegen den laufenden AppHost + `Clients/crm-web` verifiziert (zuletzt gegen eine frisch gestartete, leere Datenbank): **7 von 7 Tests gruen** (Anlegen gueltig, E-Mail-Konflikt, Leistung zuordnen, Wochenarbeitszeit hinterlegen [Bug 6 behoben], Urlaub fuer einen terminfreien Zeitraum eintragen, Urlaub kollidiert mit bestehendem Termin [beide zuvor durch Bug 7/8 blockiert, jetzt behoben und verifiziert], Deaktivieren). Bug 8 (fehlender `Authorization`-Header beim Service-zu-Service-Call `AddAbsenceHandler` → `IAppointmentAvailabilityClient`) ist vollstaendig behoben und verifiziert — siehe `Tests/E2E/README.md`.
- [x] 5. Doku/OpenAPI aktualisiert (doc-agent, 2026-10-01) — Swagger/OpenAPI über Swashbuckle (`/swagger`, nur `Development`), ergänzt um JWT-Bearer-Security-Scheme, SwaggerDoc-Titel/Beschreibung und XML-Doc-Kommentare auf Controller-/Endpoint-Ebene (`EmployeeService.Web.csproj`: `GenerateDocumentationFile`, `Program.cs`: `IncludeXmlComments`). Root-`README.md` und neues `Services/employee-service/README.md` (Zweck, Endpoints, DB, Abhängigkeiten) erstellt. Abschnitt 5 (Endpoints/Rollen/Ownership-Check bei Abwesenheiten) wurde gegen `EmployeesController.cs` geprüft — keine Abweichung gefunden.
- [x] 6. Aspire AppHost-Ressource für `employee-service` + `employee_db` gemäß ADR 0001 registrieren (devops-agent) — `AppHost/Crm.AppHost/AppHost.cs`: `AddPostgres("employee-postgres").AddDatabase("employee-db", databaseName: "employee_db")`, referenziert von `employee-service` via `WithReference(employeeDb, connectionName: "employee_db")`; End-to-End lokal mit Docker verifiziert (Container startet, Service erreicht Zustand `Running`)
- [x] 7. Polly-Resilienzrichtlinien (Timeout/Retry/Circuit-Breaker) für den synchronen Call `employee-service` → `appointment-service` (Konfliktabgleich) implementieren (backend-agent, ADR 0001) — Client-Implementierung steht bereits, da `appointment-service` selbst noch nicht existiert (siehe bekannte Lücke unten); in Tests durch Fake ersetzt.
- [x] 8. Wolverine-Event-Schema für `EmployeeDeactivated` definieren und publizieren (backend-agent, ADR 0001)
- [x] 9. YARP-Gateway-Route für `employee-service` inkl. Auth-Termination konfigurieren (devops-agent/sec-agent, ADR 0001) — `Gateway/Crm.Gateway`: Route `/api/employees/{**catch-all}` → Cluster `employee-service-cluster` (Ziel via Aspire Service Discovery, `http://employee-service`), JWT-Auth-Termination (`Authentication:JwtBearer`, Policy `gateway-authenticated`); Authority/Audience-Feinabstimmung mit sec-agent noch offen (Authority aktuell leer, analog zu den Services)

### Bekannte Lücke (backend-agent)
`appointment-service` existiert noch nicht. `IAppointmentAvailabilityClient` (Application-Layer) und dessen HTTP-Implementierung `AppointmentAvailabilityHttpClient` (Infrastructure-Layer, mit Polly-Timeout/Retry/Circuit-Breaker via `Microsoft.Extensions.Http.Resilience`) sind vollständig implementiert und werden von `AddAbsenceCommand`/`GetEmployeeAvailabilityQuery` verwendet, aber der exakte Response-Vertrag von `appointment-service` ist noch nicht spezifiziert. Es wurde der im Proposal genannte Mindestvertrag angenommen ("Liste bestehender Termine im Zeitraum X-Y für Mitarbeiter Z", Route `GET /api/appointments?employeeId=&from=&to=`, DTO `{Date, StartTime, EndTime}`). Sobald `appointment-service` (Change `terminverwaltung.openspec.md`) implementiert ist, muss `AppointmentAvailabilityHttpClient` ggf. an den tatsächlichen Contract angepasst werden. In den Tests wird die Abhängigkeit über einen Fake (`FakeAppointmentAvailabilityClient`) ersetzt.

## 9. Acceptance Criteria (DoD)
- [x] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün (siehe Task 4, 7/7 E2E gruen).
- [x] Backend API (C# 14) ist implementiert und getestet (TUnit).
- [x] Angular 22 Komponente ist implementiert und gestylt (DaisyUI 5).
- [x] E2E Tests (QA Agent) sind grün (siehe Task 4).
- [x] Swagger/OpenAPI Compliance ist gegeben (siehe Task 5, doc-agent 2026-10-01).
- [ ] Spec ist archiviert (Status `archived`, Requirements ggf. in `UserStories/specs/employee-management.openspec.md` übernommen) — Entscheidung/Durchführung liegt beim po-agent; alle Tasks sind nun abgeschlossen. Die in Abschnitt "Bekannte Lücke" dokumentierte Annahme wurde vom doc-agent (2026-10-01) gegen den inzwischen implementierten `appointment-service` verifiziert: `AppointmentListItemDto` (echter Response-Contract von `GET /api/appointments`) enthält weiterhin `Date`/`StartTime`/`EndTime` neben den zusätzlichen Feldern, der angenommene Mindestvertrag ist also tatsächlich kompatibel — kein offener Blocker. Archivierung wird empfohlen.
