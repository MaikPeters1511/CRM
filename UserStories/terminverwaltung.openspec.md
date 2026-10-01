# Change: Terminvergabe mit Kalender

> **Status:** in-progress
> Ablage: `UserStories/terminverwaltung.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-terminverwaltung.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Kernfunktion des CRM ist die Vergabe von Terminen zwischen Kunden und Mitarbeitern mit Kollisionsprüfung und Kalenderansicht. Ohne diese Capability sind Kunden- und Mitarbeiterverwaltung nicht operativ nutzbar.
- **What Changes:**
  - Neuer eigenständiger Microservice `appointment-service` (Bounded Context "Appointment Scheduling").
  - Termin anlegen/verschieben/stornieren mit Zuordnung zu Kunde (`customer-service`) und Mitarbeiter (`employee-service`).
  - Kalenderansicht (Tag/Woche/Monat) je Mitarbeiter und Gesamtübersicht.
  - Kollisionsprüfung gegen Arbeitszeiten/Abwesenheiten (`employee-service`) und bestehende Termine.
- **Impact:** Neuer Microservice `appointment-service` mit eigener PostgreSQL-Datenbank `appointment_db`. Konsumiert Daten aus `customer-service` (Change `kundenverwaltung.openspec.md`) und `employee-service` (Change `mitarbeiterverwaltung.openspec.md`) und veröffentlicht Integration Events (`AppointmentCreated`, `AppointmentCancelled`, `AppointmentRescheduled`). Servicegrenzen/Kommunikationsmuster gemäß `docs/adr/0001-microservices-architektur-crm.md`. **Hinweis:** Die in Abschnitt 4 genannten Frontend-Komponenten werden gemäß `frontend-konsolidierung.openspec.md` aus dem eigenständigen `appointment-web` in die gemeinsame Anwendung `crm-web` migriert; die Requirements/Scenarios in Abschnitt 3 bleiben davon unberührt.

## 2. User Stories
- Als Mitarbeiter möchte ich einen Termin für einen bestehenden Kunden mit einem verfügbaren Mitarbeiter anlegen, damit Kunde und Mitarbeiter zur passenden Zeit zusammenkommen.
- Als Mitarbeiter möchte ich in einer Kalenderansicht (Tag/Woche/Monat) alle Termine eines Mitarbeiters sehen, damit ich die Auslastung erkenne.
- Als Mitarbeiter möchte ich einen Termin verschieben, damit auf kurzfristige Änderungen reagiert werden kann.
- Als Mitarbeiter möchte ich einen Termin stornieren, damit abgesagte Termine nicht mehr in der Planung erscheinen, aber nachvollziehbar bleiben.
- Als System möchte ich verhindern, dass zwei Termine für denselben Mitarbeiter im selben Zeitfenster kollidieren, damit keine Doppelbuchungen entstehen.
- Als Kunde/Mitarbeiter möchte ich eine Erinnerung vor dem Termin erhalten (E-Mail), damit Termine nicht vergessen werden.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Termin anlegen
Das System SHALL das Anlegen eines Termins mit Kunde, Mitarbeiter, Leistung, Start- und Endzeit ermöglichen.

##### Scenario: Termin im verfügbaren Zeitfenster anlegen
- **GIVEN** ein Mitarbeiter ist laut `employee-service` im gewünschten Zeitfenster verfügbar
- **WHEN** ein Nutzer einen Termin mit Kunde, Mitarbeiter und diesem Zeitfenster anlegt
- **THEN** wird der Termin mit Status `Confirmed` persistiert und ein Integration Event `AppointmentCreated` veröffentlicht

##### Scenario: Termin außerhalb der Arbeitszeit anlegen
- **GIVEN** der gewünschte Zeitraum liegt außerhalb der beim `employee-service` hinterlegten Arbeitszeiten
- **WHEN** ein Nutzer versucht, den Termin zu speichern
- **THEN** wird eine Validierungsfehlermeldung angezeigt und kein Termin persistiert

#### Requirement: Kollisionsprüfung
Das System SHALL verhindern, dass für denselben Mitarbeiter zwei sich zeitlich überlappende Termine existieren.

##### Scenario: Doppelbuchung verhindern
- **GIVEN** ein Mitarbeiter hat bereits einen Termin von 10:00–11:00 Uhr
- **WHEN** ein Nutzer für denselben Mitarbeiter einen Termin von 10:30–11:30 Uhr anlegen möchte
- **THEN** wird ein Konfliktfehler angezeigt und kein neuer Termin persistiert

#### Requirement: Termin verschieben
Das System SHALL das Verschieben eines bestehenden Termins auf ein neues, kollisionsfreies Zeitfenster ermöglichen.

##### Scenario: Termin erfolgreich verschieben
- **GIVEN** ein bestehender Termin und ein neues, kollisionsfreies Zeitfenster
- **WHEN** ein Nutzer den Termin auf das neue Zeitfenster verschiebt
- **THEN** wird der Termin aktualisiert, ein Integration Event `AppointmentRescheduled` veröffentlicht und die Änderung in der Kalenderansicht sofort sichtbar

#### Requirement: Termin stornieren
Das System SHALL das Stornieren eines Termins ermöglichen, ohne den Datensatz physisch zu löschen.

##### Scenario: Termin stornieren
- **GIVEN** ein bestehender, noch nicht stattgefundener Termin
- **WHEN** ein Nutzer den Termin storniert und einen Grund angibt
- **THEN** wird der Termin als `Status = Cancelled` markiert, ein Integration Event `AppointmentCancelled` veröffentlicht und der Zeitslot für neue Buchungen freigegeben

#### Requirement: Kalenderansicht
Das System SHALL eine Kalenderansicht mit Tages-, Wochen- und Monatsansicht pro Mitarbeiter sowie eine Gesamtübersicht aller Mitarbeiter bereitstellen.

##### Scenario: Wochenansicht eines Mitarbeiters anzeigen
- **GIVEN** ein Mitarbeiter hat mehrere Termine in der aktuellen Woche
- **WHEN** ein Nutzer die Wochenansicht für diesen Mitarbeiter öffnet
- **THEN** werden alle Termine der Woche zeitlich korrekt positioniert und mit Kunde/Leistung/Status angezeigt

#### Requirement: Sichtbarkeit von Terminen nach Rolle (RBAC)
Das System SHALL Mitarbeitern mit Rolle `Employee` ausschließlich die eigenen Termine (als zugeordneter `EmployeeId`) anzeigen; Nutzer mit Rolle `Admin` SHALL alle Termine aller Mitarbeiter (Gesamtübersicht) sehen können.

##### Scenario: Employee sieht nur eigene Termine
- **GIVEN** ein Mitarbeiter A mit Rolle `Employee` ist eingeloggt und Mitarbeiter B hat ebenfalls Termine im selben Zeitraum
- **WHEN** Mitarbeiter A die Kalenderansicht/`GET /api/appointments` ohne explizite Admin-Berechtigung aufruft
- **THEN** enthält die zurückgegebene Terminliste ausschließlich Termine, bei denen `EmployeeId` der eigenen Mitarbeiter-Id von A entspricht — Termine von Mitarbeiter B werden nicht zurückgegeben, auch nicht bei explizitem `employeeId`-Filter auf B

##### Scenario: Admin sieht alle Termine
- **GIVEN** mehrere Mitarbeiter haben Termine im selben Zeitraum
- **WHEN** ein Nutzer mit Rolle `Admin` die Gesamtübersicht/`GET /api/appointments` ohne `employeeId`-Filter aufruft
- **THEN** enthält die zurückgegebene Terminliste die Termine aller Mitarbeiter

##### Scenario: Employee versucht Termin eines anderen Mitarbeiters zu verschieben/stornieren
- **GIVEN** ein Termin ist Mitarbeiter B zugeordnet
- **WHEN** Mitarbeiter A (Rolle `Employee`, nicht `Admin`) versucht, diesen Termin zu verschieben oder zu stornieren
- **THEN** wird die Anfrage mit `403 Forbidden` abgelehnt und der Termin bleibt unverändert

#### Requirement: Terminerinnerung
Das System SHALL eine automatische Erinnerung (E-Mail) an Kunde und Mitarbeiter vor Terminbeginn versenden.

##### Scenario: Erinnerung 24 Stunden vorher
- **GIVEN** ein bestätigter Termin beginnt in 24 Stunden
- **WHEN** der Erinnerungs-Job läuft
- **THEN** erhält der Kunde und der Mitarbeiter eine E-Mail-Erinnerung mit Termindetails, sofern nicht bereits storniert

## 4. UI/UX Requirements
- **Komponenten**: `calendar-view.component.ts` (Tag/Woche/Monat-Umschalter, DaisyUI `tabs` + eigenes Grid oder Angular-Kalender-Library), `appointment-form.component.ts` (Kunde-Autocomplete via `customer-service`, Mitarbeiter-/Leistungsauswahl via `employee-service`, Zeitfenster-Picker mit Live-Verfügbarkeitsanzeige), `appointment-detail.component.ts` (Verschieben/Stornieren-Aktionen, DaisyUI `modal` für Bestätigungen).
- **State**: Angular Signals/State-Service für aktuellen Kalenderausschnitt (Zeitraum, Mitarbeiterfilter), Termin-Liste mit optimistischen Updates bei Verschieben/Stornieren und Rollback bei Konfliktfehlern (409).
- **i18n / a11y**: Alle Labels/Status (`appointment.*`) über DE/EN i18n-Keys; Kalender-Grid mit `role="grid"`/`role="gridcell"` und vollständiger Tastaturnavigation (Pfeiltasten zwischen Zeitslots, Enter zum Öffnen); Konfliktmeldungen als `role="alert"`; Farbcodierung des Terminstatus zusätzlich mit Text/Icon (kein reiner Farbcode wegen Farbenblindheit).

## 5. API & Backend Requirements
- **Service:** `appointment-service` (eigenständig deploybar, eigene PostgreSQL-Instanz `appointment_db`, eigenes Aspire-Ressourcen-Modul).
- **Authentifizierung/Autorisierung**: `[Authorize]` Standard. Rolle `Employee`: darf Termine anlegen; darf nur eigene Termine (per `EmployeeId`-Ownership-Check gegen den `NameIdentifier`-Claim, analog Abwesenheiten in `mitarbeiterverwaltung.openspec.md`) lesen/verschieben/stornieren — Zugriff auf fremde `EmployeeId` liefert `403 Forbidden`. Rolle `Admin`: uneingeschränkter Lese-/Schreibzugriff auf alle Termine (Gesamtübersicht).
- **Endpoints**:
  - `GET /api/appointments?employeeId=&customerId=&from=&to=`: Gefilterte Terminliste für Kalenderansicht
  - `GET /api/appointments/{id}`: Termindetails
  - `POST /api/appointments`: Termin anlegen (inkl. Kollisionsprüfung)
  - `PUT /api/appointments/{id}/reschedule`: Termin verschieben
  - `POST /api/appointments/{id}/cancel`: Termin stornieren
- **Modelle/Entitäten (PostgreSQL, eigene DB `appointment_db`):**
  - `Appointment`: `Id (Guid)`, `CustomerId (Guid, Referenz auf customer-service)`, `EmployeeId (Guid, Referenz auf employee-service)`, `ServiceId (Guid)`, `Start (DateTimeOffset)`, `End (DateTimeOffset)`, `Status (enum: Confirmed, Cancelled, Completed)`, `CancellationReason (string?)`, `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`
- **Inter-Service-Kommunikation:** Vor dem Anlegen/Verschieben eines Termins ruft `appointment-service` synchron `GET /api/employees/{id}/availability` beim `employee-service` auf; Kundenreferenz wird beim Anlegen über `GET /api/customers/{id}` beim `customer-service` validiert (Existenz/Aktivstatus). Integration Events (`AppointmentCreated`, `AppointmentCancelled`, `AppointmentRescheduled`) werden über einen Message Broker (z.B. via Wolverine Messaging/RabbitMQ, im Rahmen der Aspire-Orchestrierung) publiziert, u.a. für den Erinnerungs-Job.
- **Resilienz:** Aufrufe zu `customer-service`/`employee-service` SHALL mit Retry-Policy (Polly) und Timeout/Circuit-Breaker abgesichert sein; bei Nichtverfügbarkeit eines abhängigen Service wird die Terminanlage mit klarer Fehlermeldung abgelehnt (kein inkonsistenter Zwischenzustand).

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Nein.
- **Prompt/System Message**: Entfällt.

## 7. Design (Technischer Ansatz)
`appointment-service` als eigenständiger Microservice mit interner Clean-Architecture-Schichtung: Domain-Layer: `Appointment` als Aggregate Root mit Invarianten zur Kollisionsprüfung (keine Infrastruktur-Abhängigkeit; Verfügbarkeitsdaten werden als Parameter/Snapshot in die Domain-Logik übergeben, nicht per direktem Fremdservice-Call aus der Domain). Application-Layer: CQRS Commands (`CreateAppointmentCommand`, `RescheduleAppointmentCommand`, `CancelAppointmentCommand`) und Queries (`GetCalendarQuery`) via Wolverine, inkl. Application-seitiger Orchestrierung der Aufrufe zu `customer-service`/`employee-service`. Infrastructure: EF Core Repository gegen `appointment_db`, HTTP-Clients (mit Polly-Resilienz) für Inter-Service-Calls, Messaging-Publisher für Integration Events. Der Erinnerungs-Job läuft als Hintergunddienst (z.B. Hosted Service/Aspire Worker) und konsumiert `AppointmentCreated`/`AppointmentCancelled`. Die Gesamt-Microservices-Topologie (Servicegrenzen zwischen `customer-service`, `employee-service`, `appointment-service`, API-Gateway, Message Broker, Datenkonsistenz-/Saga-Strategie bei fehlschlagenden Cross-Service-Validierungen) ist dokumentiert in `docs/adr/0001-microservices-architektur-crm.md`.

## 8. Tasks (Implementierungs-Checkliste)
- [x] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/` (`Tests/AppointmentService.Tests`, 34 Tests: Domain/Application/Web)
- [x] 2. Backend implementieren (Domain → Application → Infrastructure → Web) – `Services/appointment-service/`
- [x] 3. Frontend implementieren (Angular 22, DaisyUI 5, i18n, ARIA) – `Clients/appointment-web/`, 45 Tests in `Tests/AppointmentWeb.Tests/` (frontend-agent). `calendar-view.component.ts` (Tag/Woche/Monat, Mitarbeiterfilter, Grid mit `role="grid"`/`role="gridcell"` + Pfeiltasten/Enter-Navigation), `appointment-form.component.ts` (Kunde-Autocomplete gegen `customer-service`, Mitarbeiter-/Leistungsauswahl + Live-Verfuegbarkeit gegen `employee-service`), `appointment-detail.component.ts` (Verschieben/Stornieren mit DaisyUI-Bestaetigungsmodal). State via `CalendarStateService` (Signals, optimistische Updates mit Rollback bei 409). Login/Auth-Flow 1:1 von `customer-web`/`employee-web` uebernommen.
- [x] 4. E2E-Tests (QA) geschrieben UND vollstaendig gruen verifiziert (qa-agent, finaler Lauf 2026-10-01 nach Bug-9/10-Fixes) — `Tests/E2E/tests/appointments.spec.ts` (6 Tests) und `Tests/E2E/tests/rbac.spec.ts` (5 Tests, inkl. dem ehemals `test.fixme`-markierten positiven Ownership-Fall). Real gegen den laufenden AppHost + `Clients/crm-web` verifiziert, kompletter AppHost-Neustart mit frischer, leerer Datenbank unmittelbar vor dem massgeblichen Lauf: **11 von 11 Tests gruen** (Gesamt-Suite 30/30). Bug 8 (fehlender Authorization-Header), Bug 9 (fehlendes UTC-Offset-Handling bei Terminanlage/-verschiebung) und Bug 10 (Mitarbeiter-Pagination im Formular/Filter) sind alle vollstaendig behoben und verifiziert — kein Test scheitert mehr daran. Vier zusaetzlich im Testcode selbst entdeckte und vom qa-agent korrigierte Probleme (kein Produktbug): fehlender Klick auf das Autocomplete-Suchergebnis bei der Kundenauswahl, falsche i18n-Button-Beschriftung ("Weiter" statt "Naechste") in der Testsuite, mehrdeutiger `getByText`-Locator im Kalendergrid (jede Zelle zeigt die Stundenbeschriftung, nicht nur Zellen mit Termin), Off-by-one-Week in der Testdaten-Hilfsfunktion `nextWeekday()`. Details siehe `Tests/E2E/README.md`, Abschnitt "Tatsaechliche Verifikation - finaler Stand".
- [x] 5. Doku/OpenAPI aktualisiert (doc-agent, 2026-10-01) — Swagger/OpenAPI über Swashbuckle (`/swagger`, nur `Development`), ergänzt um JWT-Bearer-Security-Scheme, SwaggerDoc-Titel/Beschreibung und XML-Doc-Kommentare auf Controller-/Endpoint-Ebene inkl. RBAC-Hinweisen (`AppointmentService.Web.csproj`: `GenerateDocumentationFile`, `Program.cs`: `IncludeXmlComments`). Root-`README.md` und neues `Services/appointment-service/README.md` (Zweck, Endpoints, DB, Abhängigkeiten) erstellt. Abschnitt 5 (Endpoints/RBAC) wurde gegen `AppointmentsController.cs` geprüft — Ownership-Filter/403-Verhalten entspricht exakt der Spec, keine Abweichung gefunden. Zusätzlich verifiziert: `AppointmentListItemDto` (echter Response-Contract von `GET /api/appointments`) ist mit dem in `mitarbeiterverwaltung.openspec.md` als "Bekannte Lücke" angenommenen Mindestvertrag (`Date`/`StartTime`/`EndTime`) kompatibel (zusätzliche Felder werden vom `employee-service`-Client ignoriert) — keine Korrektur nötig.
- [x] 6. Aspire AppHost-Ressource für `appointment-service` + `appointment_db` + RabbitMQ gemäß ADR 0001 registrieren (backend-agent, siehe `AppHost/Crm.AppHost/AppHost.cs`)
- [x] 7. HTTP-Client-Contracts mit Polly-Resilienz (Timeout ~2-3s, max. 2 Retries, Circuit-Breaker) für Calls zu `customer-service`/`employee-service` implementieren (backend-agent, ADR 0001; siehe `AppointmentService.Infrastructure/Clients/` + `DependencyInjection.cs`)
- [x] 8. Wolverine-Event-Schemas für `AppointmentCreated`/`AppointmentCancelled`/`AppointmentRescheduled` definieren und publizieren; Erinnerungs-Job als Hosted Service konsumiert diese (backend-agent, ADR 0001; siehe `AppointmentService.Application/Appointments/IntegrationEvents/` + `AppointmentService.Infrastructure/BackgroundJobs/AppointmentReminderHostedService.cs`). Erinnerungs-Job ist ein einfacher Polling-Hosted-Service mit Log-Stub-Notifier (`IReminderNotifier`/`LogReminderNotifier`), kein echter E-Mail-Versand (bewusste Vereinfachung, kein Notification-Service im Projekt vorhanden).
- [x] 9. Kompensationslogik `RequiresReview`-Status implementieren für den Fall, dass ein referenzierter Kunde während der Terminanlage deaktiviert wird (backend-agent, ADR 0001; siehe `Appointment.MarkRequiresReview` + `CustomerDeactivatedConsumer`, konsumiert das von `customer-service` publizierte `CustomerDeactivated`-Integration-Event)
- [x] 10. YARP-Gateway-Route für `appointment-service` inkl. Auth-Termination konfigurieren (backend-agent, ADR 0001; siehe `Gateway/Crm.Gateway/appsettings.json`, Route `/api/appointments/**`)
- [x] 11a. RBAC Backend-Teil "Employee sieht nur eigene Termine, Admin sieht alle" umsetzen: Ownership-Filter in `GetCalendarQuery`/`GetCalendarHandler` und `GetAppointmentByIdQuery`/`GetAppointmentByIdHandler` (client-seitiger `employeeId`-Filter wird für Rolle `Employee` ignoriert/überschrieben) sowie 403-Check (neuer `Forbidden`-Status, gemappt in `AppointmentsController`) in `RescheduleAppointmentCommand`/-Handler und `CancelAppointmentCommand`/-Handler (backend-agent; siehe `AppointmentService.Application/Appointments/Calendar/`, `.../GetById/`, `.../Reschedule/`, `.../Cancel/` und `AppointmentService.Web/Controllers/AppointmentsController.cs`; Tests in `Tests/AppointmentService.Tests/Application/` und `Tests/AppointmentService.Tests/Web/AppointmentsApiTests.cs`)
- [x] 11b. RBAC Frontend-Teil: Mitarbeiterfilter in `Clients/appointment-web` für `Employee`-Rolle ausgeblendet (`calendar-view.component.ts`/`.html`, `isAdmin`-Guard via `AuthStateService`); saubere i18n-Fehlermeldung `appointment.errors.forbidden` bei 403 beim Verschieben/Stornieren fremder Termine inkl. Rollback des optimistischen Updates (`calendar-state.service.ts`, `appointment-detail.component.ts`); 5 neue Tests in `Tests/AppointmentWeb.Tests/` (frontend-agent)

## 9. Acceptance Criteria (DoD)
- [x] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün (siehe Task 4, 11/11 E2E gruen).
- [x] Backend API (C# 14) ist implementiert und getestet (TUnit).
- [x] Angular 22 Komponente ist implementiert und gestylt (DaisyUI 5).
- [x] E2E Tests (QA Agent) sind grün (siehe Task 4).
- [x] Swagger/OpenAPI Compliance ist gegeben (siehe Task 5, doc-agent 2026-10-01).
- [ ] Spec ist archiviert (Status `archived`, Requirements ggf. in `UserStories/specs/appointment-scheduling.openspec.md` übernommen) — Entscheidung/Durchführung liegt beim po-agent; alle Tasks sind nun abgeschlossen, Archivierung wird empfohlen.
