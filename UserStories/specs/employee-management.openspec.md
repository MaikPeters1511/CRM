# Capability: Employee Management

> **Status:** active
> Implementiert durch `employee-service` (Bounded Context "Employee Management"). Archivierte Change-Historie: `UserStories/archive/2026-10-01-mitarbeiterverwaltung.openspec.md`.

## Zweck
Verwaltung von Mitarbeitern mit Rollen, Qualifikationen/Leistungen, Arbeitszeiten und Abwesenheiten als Grundlage für die Verfügbarkeitsprüfung in der Terminvergabe.

## Requirements

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
- **THEN** wird eine Konfliktwarnung angezeigt und die Bestätigung erfordert eine explizite Bestätigung oder Terminverschiebung

#### Requirement: Mitarbeiter deaktivieren (Soft-Delete)
Das System SHALL das Deaktivieren eines Mitarbeiters ermöglichen, ohne bestehende Termine oder Historie zu löschen.

##### Scenario: Mitarbeiter deaktivieren
- **GIVEN** ein Admin betrachtet einen aktiven Mitarbeiter ohne zukünftige offene Termine
- **WHEN** er "Deaktivieren" auswählt und bestätigt
- **THEN** wird der Mitarbeiter als `IsActive = false` markiert und erscheint nicht mehr in der Verfügbarkeitsauswahl für neue Termine

## API
- `GET /api/employees?search=&role=&serviceId=&page=`, `GET /api/employees/{id}`, `POST /api/employees`, `PUT /api/employees/{id}`, `POST /api/employees/{id}/deactivate`, `PUT /api/employees/{id}/schedule`, `POST /api/employees/{id}/absences`, `GET /api/employees/{id}/availability?from=&to=`.
- Rollen: `Admin` (Anlegen/Bearbeiten/Deaktivieren/Rollenzuweisung); `Employee` darf nur eigene Abwesenheiten anlegen (Ownership-Check gegen `NameIdentifier`-Claim).
- Siehe `Services/employee-service/README.md` und Swagger (`/swagger`, Development).

## Architektur-Verweise
- `docs/adr/0001-microservices-architektur-crm.md`
- `docs/adr/0002-frontend-konsolidierung.md` (Feature-Bereich `employees` in `Clients/crm-web`)
- Integration Event: `EmployeeDeactivated`
- Inter-Service: `GetEmployeeAvailabilityQuery` ruft synchron `appointment-service` (Polly-Resilienz) für die Konfliktprüfung.

## Tests
- `Tests/EmployeeService.Tests` (Backend, inkl. echter Infrastruktur-Tests gegen Testcontainers-PostgreSQL), `Tests/CrmWeb.Tests` (Frontend, Feature-Bereich `employees`), `Tests/E2E/tests/employees.spec.ts` (7/7 grün).
