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
Das System SHALL die Zuordnung von einer oder mehreren Leistungen zu einem Mitarbeiter durch Auswahl aus dem zentralen Leistungskatalog (`catalog-service`, siehe `UserStories/specs/service-catalog.openspec.md`) ermöglichen — keine manuelle Eingabe von `ServiceId`/`ServiceName`. Bereits zugeordnete Leistungen bleiben als Snapshot (`ServiceId`+`ServiceName` zum Zeitpunkt der Zuordnung) unverändert sichtbar, auch wenn die Leistung im Katalog später deaktiviert wird.

##### Scenario: Leistung aus Katalog zuordnen
- **GIVEN** ein bestehender Mitarbeiter und eine aktive Leistung "Beratung" existieren im Leistungskatalog
- **WHEN** ein Admin die Leistung im Mitarbeiterformular aus der Auswahlliste wählt und speichert
- **THEN** wird die Zuordnung mit der `ServiceId` und dem aktuellen `ServiceName` aus dem Katalog gespeichert, und der Mitarbeiter erscheint bei der Terminvergabe als möglicher Ansprechpartner für "Beratung"

##### Scenario: Bereits zugeordnete, inzwischen deaktivierte Leistung bleibt sichtbar
- **GIVEN** ein Mitarbeiter hat eine Leistung zugeordnet, die inzwischen im Katalog deaktiviert wurde
- **WHEN** der Admin das Mitarbeiterformular öffnet
- **THEN** bleibt die bestehende Zuordnung unverändert sichtbar (Snapshot-Name), ist aber nicht erneut aus der Auswahlliste neu zuordenbar

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
- Frontend-Abhängigkeit (nur Lesezugriff, kein Backend-zu-Backend-Call): Das Mitarbeiterformular (`employee-form.component.ts`) ruft `catalog-service` direkt über das Gateway auf (`GET /api/services?status=true`), um die Leistungsauswahl zu befüllen — siehe `UserStories/specs/service-catalog.openspec.md`.

## Tests
- `Tests/EmployeeService.Tests` (Backend, inkl. echter Infrastruktur-Tests gegen Testcontainers-PostgreSQL), `Tests/CrmWeb.Tests` (Frontend, Feature-Bereich `employees`), `Tests/E2E/tests/employees.spec.ts` (7/7 grün).
