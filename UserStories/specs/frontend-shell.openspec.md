# Capability: Frontend Shell

> **Status:** active
> Implementiert durch die konsolidierte Angular-Anwendung `Clients/crm-web`. Archivierte Change-Historie: `UserStories/archive/2026-10-01-frontend-konsolidierung.openspec.md`.

## Zweck
Eine einzige Angular-Anwendung mit gemeinsamer Shell (Login, Navigation, Session), die alle fachlichen Feature-Bereiche (Kunden, Mitarbeiter, Termine) vereint — statt eines Clients pro Backend-Microservice.

## Requirements

#### Requirement: Einheitliche Frontend-Anwendung
Das System SHALL eine einzige Angular-Anwendung bereitstellen, die Kunden-, Mitarbeiter- und Terminverwaltung als Feature-Bereiche einer gemeinsamen Shell vereint.

##### Scenario: Wechsel zwischen Modulen ohne erneuten Login
- **GIVEN** ein eingeloggter Nutzer befindet sich im Kunden-Bereich
- **WHEN** er über die gemeinsame Navigation in den Termin-Bereich wechselt
- **THEN** wird der Termin-Bereich geladen, ohne dass ein erneuter Login nötig ist, und die bestehende Session/Rolle bleibt erhalten

#### Requirement: Gemeinsame Navigation
Das System SHALL eine gemeinsame Navigationsleiste bereitstellen, über die alle Bereiche erreichbar sind, für die der eingeloggte Nutzer laut Rolle berechtigt ist.

##### Scenario: Navigation zeigt alle drei Bereiche für Admin/Employee
- **GIVEN** ein Nutzer mit Rolle `Admin` oder `Employee` ist eingeloggt
- **WHEN** die Anwendung geladen wird
- **THEN** zeigt die Navigation Einträge für Kunden, Mitarbeiter und Termine

#### Requirement: Regressionsfreie Migration bestehender Funktionalität
Das System SHALL alle fachlichen Requirements der drei Capabilities (`customer-management`, `employee-management`, `appointment-scheduling`) unverändert erfüllen.

##### Scenario: Bestehende Scenarios bleiben gültig
- **GIVEN** alle Scenarios der drei Fach-Capabilities
- **WHEN** sie gegen die konsolidierte Anwendung `crm-web` ausgeführt werden
- **THEN** verhalten sie sich identisch zu den ursprünglichen Einzel-Apps (keine fachliche Regression)

## Struktur
- Shell-Komponente (Header/Navigation/Router-Outlet), Lazy-Routes `/customers/**`, `/employees/**`, `/appointments/**`, `/login`.
- Ein gemeinsamer Auth-Flow (`AuthStateService`, `authInterceptor`, `authGuard`, `LoginComponent`) für die gesamte App.
- Zusammengeführte i18n (`public/i18n/{de,en}.json`, Keys `customer.*`/`employee.*`/`appointment.*`/`auth.*`).
- Spricht ausschließlich über das YARP-Gateway mit `identity-service`, `customer-service`, `employee-service`, `appointment-service`.

## Bewusste Architekturabweichung
Diese Konsolidierung weicht bewusst von der ursprünglichen "ein Client pro Microservice"-Praxis ab (Presentation-Layer-Entscheidung, Backend-Entkopplung bleibt vollständig erhalten). Begründung und Abgrenzung: `docs/adr/0002-frontend-konsolidierung.md`.

## Tests
- `Tests/CrmWeb.Tests` (122 Unit-/Komponententests, inkl. `shell/app.spec.ts` für Navigation/Session-Erhalt).
- `Tests/E2E/` (30/30 Tests grün, gegen `crm-web` als einzige Basis-URL).
