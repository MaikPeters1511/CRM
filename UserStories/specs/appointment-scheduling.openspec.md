# Capability: Appointment Scheduling

> **Status:** active
> Implementiert durch `appointment-service` (Bounded Context "Appointment Scheduling"). Archivierte Change-Historie: `UserStories/archive/2026-10-01-terminverwaltung.openspec.md`.

## Zweck
Vergabe von Terminen zwischen Kunden und Mitarbeitern mit Kollisionsprüfung, Kalenderansicht und rollenbasierter Sichtbarkeit.

## Requirements

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
- **THEN** enthält die zurückgegebene Terminliste ausschließlich Termine, bei denen `EmployeeId` der eigenen Mitarbeiter-Id von A entspricht — auch nicht bei explizitem `employeeId`-Filter auf B

##### Scenario: Admin sieht alle Termine
- **GIVEN** mehrere Mitarbeiter haben Termine im selben Zeitraum
- **WHEN** ein Nutzer mit Rolle `Admin` die Gesamtübersicht ohne `employeeId`-Filter aufruft
- **THEN** enthält die zurückgegebene Terminliste die Termine aller Mitarbeiter

##### Scenario: Employee versucht Termin eines anderen Mitarbeiters zu verschieben/stornieren
- **GIVEN** ein Termin ist Mitarbeiter B zugeordnet
- **WHEN** Mitarbeiter A (Rolle `Employee`, nicht `Admin`) versucht, diesen Termin zu verschieben oder zu stornieren
- **THEN** wird die Anfrage mit `403 Forbidden` abgelehnt und der Termin bleibt unverändert

#### Requirement: Terminerinnerung
Das System SHALL eine automatische Erinnerung an Kunde und Mitarbeiter vor Terminbeginn versenden.

##### Scenario: Erinnerung 24 Stunden vorher
- **GIVEN** ein bestätigter Termin beginnt in 24 Stunden
- **WHEN** der Erinnerungs-Job läuft
- **THEN** erhält der Kunde und der Mitarbeiter eine Erinnerung mit Termindetails, sofern nicht bereits storniert (Hinweis: aktuell Log-Stub `IReminderNotifier`, kein echter E-Mail-Versand)

## API
- `GET /api/appointments?employeeId=&customerId=&from=&to=`, `GET /api/appointments/{id}`, `POST /api/appointments`, `PUT /api/appointments/{id}/reschedule`, `POST /api/appointments/{id}/cancel`.
- Rollen: `Employee` (eigene Termine anlegen/lesen/verschieben/stornieren), `Admin` (uneingeschränkt).
- Siehe `Services/appointment-service/README.md` und Swagger (`/swagger`, Development).

## Architektur-Verweise
- `docs/adr/0001-microservices-architektur-crm.md` (Polly-Resilienz für Cross-Service-Calls, Kompensation statt Saga)
- `docs/adr/0002-frontend-konsolidierung.md` (Feature-Bereich `appointments` in `Clients/crm-web`)
- Kompensation: `RequiresReview`-Status bei nachträglicher Kundendeaktivierung (`CustomerDeactivatedConsumer`)
- Inter-Service-Calls tragen den Authorization-Header des eingehenden Requests weiter (`AuthHeaderForwardingHandler`)

## Tests
- `Tests/AppointmentService.Tests` (Backend), `Tests/CrmWeb.Tests` (Frontend, Feature-Bereich `appointments`), `Tests/E2E/tests/appointments.spec.ts` + `rbac.spec.ts` (11/11 grün).
