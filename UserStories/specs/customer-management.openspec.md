# Capability: Customer Management

> **Status:** active
> Implementiert durch `customer-service` (Bounded Context "Customer Management"). Archivierte Change-Historie: `UserStories/archive/2026-10-01-kundenverwaltung.openspec.md`.

## Zweck
Zentrale Verwaltung von Kundendaten als Grundlage für Terminvergabe, Kommunikation und Reporting.

## Requirements

#### Requirement: Kunde anlegen
Das System SHALL das Anlegen eines neuen Kunden mit Pflichtfeldern (Vorname, Nachname, E-Mail oder Telefonnummer) ermöglichen.

##### Scenario: Kunde mit gültigen Pflichtdaten anlegen
- **GIVEN** ein Mitarbeiter ist authentifiziert und befindet sich im Kundenmodul
- **WHEN** er die Pflichtfelder gültig ausfüllt und speichert
- **THEN** wird ein neuer Kundendatensatz mit eindeutiger Id persistiert und in der Kundenliste angezeigt

##### Scenario: Kunde ohne Pflichtfelder anlegen
- **GIVEN** ein Mitarbeiter befindet sich im Anlage-Formular
- **WHEN** er ohne Vor-/Nachname speichert
- **THEN** wird eine Validierungsfehlermeldung angezeigt und kein Datensatz persistiert

#### Requirement: Kunden suchen und filtern
Das System SHALL eine Volltextsuche über Name, E-Mail und Telefonnummer sowie Filterung nach Status (aktiv/inaktiv) bereitstellen.

##### Scenario: Suche nach Nachnamen
- **GIVEN** mehrere Kunden existieren
- **WHEN** ein Mitarbeiter einen Nachnamen in die Suche eingibt
- **THEN** werden nur Kunden angezeigt, deren Nachname den Suchbegriff enthält

#### Requirement: Kunde bearbeiten
Das System SHALL das Bearbeiten bestehender Kundendaten ermöglichen und Änderungen versioniert nachvollziehbar machen (Audit-Felder: geändert von/am).

##### Scenario: Kundendaten aktualisieren
- **GIVEN** ein bestehender Kunde ist geöffnet
- **WHEN** ein Mitarbeiter ein Feld ändert und speichert
- **THEN** werden die Änderungen persistiert und `ModifiedAt`/`ModifiedBy` aktualisiert

#### Requirement: Kunde deaktivieren (Soft-Delete)
Das System SHALL das Deaktivieren eines Kunden ermöglichen, ohne den Datensatz oder verknüpfte Termine physisch zu löschen.

##### Scenario: Kunden deaktivieren
- **GIVEN** ein Admin betrachtet einen aktiven Kunden
- **WHEN** er "Deaktivieren" auswählt und bestätigt
- **THEN** wird der Kunde als `IsActive = false` markiert und erscheint nicht mehr in der Standard-Kundenliste, bleibt aber über den Filter "Inaktiv" sichtbar

#### Requirement: Kundendetailsicht mit Terminhistorie
Das System SHALL in der Kundendetailsicht alle zugeordneten Termine (vergangen/zukünftig) anzeigen.

##### Scenario: Terminhistorie anzeigen
- **GIVEN** ein Kunde hat mindestens einen vergangenen und einen zukünftigen Termin
- **WHEN** ein Mitarbeiter die Kundendetailsicht öffnet
- **THEN** werden beide Termine chronologisch sortiert mit Status angezeigt

## API
- `GET /api/customers?search=&status=&page=`, `GET /api/customers/{id}`, `POST /api/customers`, `PUT /api/customers/{id}`, `POST /api/customers/{id}/deactivate`.
- Rollen: `Employee` (Lesen/Anlegen/Bearbeiten), `Admin` (zusätzlich Deaktivieren).
- Siehe `Services/customer-service/README.md` und Swagger (`/swagger`, Development).

## Architektur-Verweise
- `docs/adr/0001-microservices-architektur-crm.md` (Servicegrenzen, Database-per-Service, Kommunikationsmuster)
- `docs/adr/0002-frontend-konsolidierung.md` (Frontend-Komponenten sind Teil von `Clients/crm-web`, Feature-Bereich `customers`)
- Integration Event: `CustomerDeactivated` (konsumiert u.a. von `appointment-service` für die `RequiresReview`-Kompensation)

## Tests
- `Tests/CustomerService.Tests` (Backend), `Tests/CrmWeb.Tests` (Frontend, Feature-Bereich `customers`), `Tests/E2E/tests/customers.spec.ts` (6/6 grün).
