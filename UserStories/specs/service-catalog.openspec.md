# Capability: Service Catalog

> **Status:** active
> Implementiert durch `catalog-service` (Bounded Context "Service Catalog"). Archivierte Change-Historie: `UserStories/archive/2026-11-01-leistungskatalog.openspec.md`.

## Zweck
Zentrale, konsistente Verwaltung der im Unternehmen angebotenen Leistungen ("Services") als Grundlage für die Leistungszuordnung zu Mitarbeitern (`employee-management`). Löst die ursprüngliche manuelle GUID+Name-Eingabe ab.

## Requirements

#### Requirement: Leistung anlegen
Das System SHALL das Anlegen einer Leistung mit Pflichtfeld Name ermöglichen (Beschreibung und Standarddauer optional).

##### Scenario: Leistung mit gültigem Namen anlegen
- **GIVEN** ein Admin ist authentifiziert und befindet sich im Leistungskatalog-Modul
- **WHEN** er einen Namen eingibt und speichert
- **THEN** wird eine neue Leistung mit eindeutiger Id persistiert und in der Leistungsliste angezeigt

##### Scenario: Leistung ohne Namen anlegen
- **GIVEN** ein Admin befindet sich im Anlage-Formular
- **WHEN** er ohne Namen speichert
- **THEN** wird eine Validierungsfehlermeldung angezeigt und keine Leistung persistiert

#### Requirement: Leistungen suchen und auflisten
Das System SHALL eine durchsuchbare, paginierte Liste aller Leistungen (inkl. Filter nach aktiv/inaktiv) bereitstellen.

##### Scenario: Suche nach Namen
- **GIVEN** mehrere Leistungen existieren
- **WHEN** ein Nutzer einen Namen in die Suche eingibt
- **THEN** werden nur Leistungen angezeigt, deren Name den Suchbegriff enthält

#### Requirement: Leistung bearbeiten
Das System SHALL das Bearbeiten einer bestehenden Leistung ermöglichen.

##### Scenario: Leistung aktualisieren
- **GIVEN** eine bestehende Leistung ist geöffnet
- **WHEN** ein Admin ein Feld ändert und speichert
- **THEN** werden die Änderungen persistiert

#### Requirement: Leistung deaktivieren (Soft-Delete)
Das System SHALL das Deaktivieren einer Leistung ermöglichen, ohne sie oder bestehende Mitarbeiter-Zuordnungen physisch zu löschen.

##### Scenario: Leistung deaktivieren
- **GIVEN** ein Admin betrachtet eine aktive Leistung
- **WHEN** er "Deaktivieren" auswählt und bestätigt
- **THEN** wird die Leistung als `IsActive = false` markiert, erscheint nicht mehr in der Standardliste/Auswahl für neue Zuordnungen, bleibt aber über den Filter "Inaktiv" sichtbar und bestehende Mitarbeiter-Zuordnungen bleiben unverändert

#### Requirement: Integration Event bei Katalog-Änderungen (MODIFIED)
Das System SHALL beim Anlegen, Bearbeiten oder Deaktivieren einer Leistung ein asynchrones Integration Event `ServiceCatalogChangedIntegrationEvent` veröffentlichen (Name, Beschreibung, Standarddauer, `IsActive`, Änderungstyp Created/Updated/Deactivated, Zeitstempel), damit Konsumenten (aktuell `ai-service`, siehe `docs/adr/0003-ai-service-qdrant-index-strategie.md`) ihren eigenen Datenbestand ohne Polling aktuell halten können. Ersetzt die bisherige Festlegung "kein Integration-Event-Publishing" (archivierte `leistungskatalog.openspec.md`), da mit `ai-service` erstmals ein Konsument existiert.

##### Scenario: Leistung anlegen veröffentlicht Event
- **GIVEN** ein Admin legt eine neue Leistung an
- **WHEN** die Leistung erfolgreich persistiert wurde
- **THEN** veröffentlicht `catalog-service` ein `ServiceCatalogChangedIntegrationEvent` mit Änderungstyp `Created`

##### Scenario: Leistung deaktivieren veröffentlicht Event
- **GIVEN** ein Admin deaktiviert eine bestehende Leistung
- **WHEN** die Deaktivierung erfolgreich persistiert wurde
- **THEN** veröffentlicht `catalog-service` ein `ServiceCatalogChangedIntegrationEvent` mit Änderungstyp `Deactivated` und `IsActive = false`

## API
- `GET /api/services?search=&status=&page=` → `{ items: ServiceDto[], totalCount, page, pageSize }`; `GET /api/services/{id}`; `POST /api/services`; `PUT /api/services/{id}`; `POST /api/services/{id}/deactivate`.
- `ServiceDto`: `{ id, name, description, defaultDurationMinutes, isActive, createdAt, createdBy, modifiedAt, modifiedBy }`.
- Rollen: Lesen (`GET`) für `Employee`/`Admin`; Schreiben (`POST`/`PUT`/`.../deactivate`) nur `Admin`.
- Siehe `Services/catalog-service/README.md` und Swagger (`/swagger`, Development).

## Architektur-Verweise
- `docs/adr/0001-microservices-architektur-crm.md` (Database-per-Service, eigene DB `catalog_db`)
- `docs/adr/0002-frontend-konsolidierung.md` (Feature-Bereich `catalog` in `Clients/crm-web`, nur für Admin sichtbarer Navigationspunkt)
- Integration-Event-Publishing (`ServiceCatalogChangedIntegrationEvent`, Rabbit-Exchange `service-catalog-changed`) seit `docs/adr/0003-ai-service-qdrant-index-strategie.md`; zuvor bewusst ohne Events (kein Konsument, kein Gold-Plating) - siehe archivierte `leistungskatalog.openspec.md`.
- Kein synchroner Backend-zu-Backend-Call von `employee-service`/`appointment-service` — nur `crm-web` ruft `catalog-service` direkt über das Gateway auf. `employee-service` übernimmt `ServiceId`/`ServiceName` als Snapshot-Werte zum Zeitpunkt der Zuordnung (siehe `UserStories/specs/employee-management.openspec.md`, Requirement "Leistungen/Qualifikationen zuordnen") — eine spätere Deaktivierung im Katalog wirkt sich nicht rückwirkend auf bestehende Zuordnungen aus.
- Bekannte Beobachtung (kein Bug, siehe `Tests/E2E/README.md`): Die Leistungsauswahl im Mitarbeiterformular lädt ohne explizite `pageSize` (Backend-Default). Sollte der Katalog über die Standard-Seitengröße hinauswachsen, ist dieselbe Nachbesserung wie beim historischen "Bug 10" (Mitarbeiterauswahl) fällig.

## Tests
- `Tests/CatalogService.Tests` (Backend, 38 Tests inkl. Testcontainers-Regressionstest und `ServiceCatalogChangedIntegrationEvent`-Publish-Tests), `Tests/CrmWeb.Tests/catalog/` (Frontend), `Tests/E2E/tests/catalog.spec.ts` (5/5 grün) + erweiterter Leistungszuordnungs-Test in `employees.spec.ts`.
