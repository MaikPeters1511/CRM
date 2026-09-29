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
- **Impact:** Neuer Microservice `appointment-service` mit eigener PostgreSQL-Datenbank `appointment_db`. Konsumiert Daten aus `customer-service` (Change `kundenverwaltung.openspec.md`) und `employee-service` (Change `mitarbeiterverwaltung.openspec.md`) und veröffentlicht Integration Events (`AppointmentCreated`, `AppointmentCancelled`, `AppointmentRescheduled`). Servicegrenzen/Kommunikationsmuster gemäß `docs/adr/0001-microservices-architektur-crm.md`.

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
- **Authentifizierung/Autorisierung**: `[Authorize]` Standard; Rolle `Employee` für Anlegen/Verschieben/Stornieren eigener bzw. aller Termine (abhängig von RBAC-Feinheit aus `mitarbeiterverwaltung.openspec.md`), Rolle `Admin` für Gesamtübersicht.
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
- [ ] 3. Frontend implementieren (Angular 22, DaisyUI 5, i18n, ARIA)
- [ ] 4. E2E-/Compliance-Tests (QA) grün
- [ ] 5. Doku/OpenAPI aktualisiert
- [x] 6. Aspire AppHost-Ressource für `appointment-service` + `appointment_db` + RabbitMQ gemäß ADR 0001 registrieren (backend-agent, siehe `AppHost/Crm.AppHost/AppHost.cs`)
- [x] 7. HTTP-Client-Contracts mit Polly-Resilienz (Timeout ~2-3s, max. 2 Retries, Circuit-Breaker) für Calls zu `customer-service`/`employee-service` implementieren (backend-agent, ADR 0001; siehe `AppointmentService.Infrastructure/Clients/` + `DependencyInjection.cs`)
- [x] 8. Wolverine-Event-Schemas für `AppointmentCreated`/`AppointmentCancelled`/`AppointmentRescheduled` definieren und publizieren; Erinnerungs-Job als Hosted Service konsumiert diese (backend-agent, ADR 0001; siehe `AppointmentService.Application/Appointments/IntegrationEvents/` + `AppointmentService.Infrastructure/BackgroundJobs/AppointmentReminderHostedService.cs`). Erinnerungs-Job ist ein einfacher Polling-Hosted-Service mit Log-Stub-Notifier (`IReminderNotifier`/`LogReminderNotifier`), kein echter E-Mail-Versand (bewusste Vereinfachung, kein Notification-Service im Projekt vorhanden).
- [x] 9. Kompensationslogik `RequiresReview`-Status implementieren für den Fall, dass ein referenzierter Kunde während der Terminanlage deaktiviert wird (backend-agent, ADR 0001; siehe `Appointment.MarkRequiresReview` + `CustomerDeactivatedConsumer`, konsumiert das von `customer-service` publizierte `CustomerDeactivated`-Integration-Event)
- [x] 10. YARP-Gateway-Route für `appointment-service` inkl. Auth-Termination konfigurieren (backend-agent, ADR 0001; siehe `Gateway/Crm.Gateway/appsettings.json`, Route `/api/appointments/**`)

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün.
- [ ] Backend API (C# 14) ist implementiert und getestet (TUnit).
- [ ] Angular 22 Komponente ist implementiert und gestylt (DaisyUI 5).
- [ ] E2E Tests (QA Agent) sind grün.
- [ ] Swagger/OpenAPI Compliance ist gegeben.
- [ ] Spec ist archiviert (Status `archived`, Requirements ggf. in `UserStories/specs/appointment-scheduling.openspec.md` übernommen).
