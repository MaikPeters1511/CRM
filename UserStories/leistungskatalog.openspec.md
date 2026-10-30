# Change: Leistungskatalog (Service Catalog)

> **Status:** proposed
> Ablage: `UserStories/leistungskatalog.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-leistungskatalog.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Die Zuordnung von Leistungen zu Mitarbeitern (`employee-service`, `EmployeeServiceAssignment`) verlangt aktuell vom Admin, eine rohe `ServiceId` (GUID) sowie einen `ServiceName` manuell als Freitext einzutippen — es gibt keine zentrale Liste der im Unternehmen angebotenen Leistungen. Das ist faktisch unbedienbar (niemand tippt GUIDs ab) und führt zu inkonsistenten Daten (Tippfehler, doppelte Leistungen mit unterschiedlichem Namen unter derselben/verschiedener Id). Der Domain-Kommentar in `EmployeeServiceAssignment.cs` sieht diese Trennung bereits explizit vor: *"Maps a Leistung/Service (owned by a separate bounded context) to an employee"*.
- **What Changes:**
  - Neuer, eigenständiger Microservice `catalog-service` (Bounded Context "Service Catalog") mit CRUD-Verwaltung für Leistungen (Name, Beschreibung, Standarddauer, aktiv/inaktiv).
  - Neue Admin-UI-Seite "Leistungen verwalten" in `crm-web` zum Anlegen/Bearbeiten/Deaktivieren von Leistungen.
  - Die Leistungszuordnung im Mitarbeiterformular (`employee-form.component.ts`) wird von manueller GUID+Name-Eingabe auf eine Auswahl (Mehrfachauswahl aus dem Katalog) umgestellt.
- **Impact:** Neuer Microservice `catalog-service` mit eigener Datenbank `catalog_db` (Database-per-Service, ADR 0001). `employee-service` konsumiert den Katalog nur lesend für die Zuordnungs-UI (kein DB-Zugriff über Servicegrenzen hinweg) — die bestehende `EmployeeServiceAssignment`-Struktur (`ServiceId`/`ServiceName` als Snapshot-Werte) bleibt unverändert, `ServiceName` wird beim Zuordnen aus dem Katalog übernommen statt getippt. `appointment-service` ist NICHT betroffen (nutzt weiterhin die bereits zugeordneten `ServiceId`/`ServiceName`-Werte des Mitarbeiters unverändert). Gateway-Route `/api/services/**` ergänzt, Aspire-AppHost-Registrierung (`catalog-postgres`/`catalog_db`).

## 2. User Stories
- Als Admin möchte ich Leistungen (Name, Beschreibung, Standarddauer) zentral anlegen und pflegen, damit alle Mitarbeiter-Zuordnungen auf denselben, konsistenten Daten basieren.
- Als Admin möchte ich eine Leistung deaktivieren können, ohne bestehende Mitarbeiter-Zuordnungen oder vergangene Termine zu beeinträchtigen.
- Als Admin möchte ich im Mitarbeiterformular Leistungen aus einer Liste auswählen statt IDs einzutippen.

## 3. Requirements (Delta)

### ADDED Requirements

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

### MODIFIED Requirements

#### Requirement: Leistungen/Qualifikationen zuordnen
(vormals in `mitarbeiterverwaltung.openspec.md`/`UserStories/specs/employee-management.openspec.md`)

Das System SHALL die Zuordnung von einer oder mehreren Leistungen zu einem Mitarbeiter **durch Auswahl aus dem zentralen Leistungskatalog** (`catalog-service`) ermöglichen — keine manuelle Eingabe von `ServiceId`/`ServiceName` mehr.

##### Scenario: Leistung aus Katalog zuordnen
- **GIVEN** ein bestehender Mitarbeiter und eine aktive Leistung "Beratung" existieren im Katalog
- **WHEN** ein Admin die Leistung im Mitarbeiterformular aus der Auswahlliste wählt und speichert
- **THEN** wird die Zuordnung mit der `ServiceId` und dem aktuellen `ServiceName` aus dem Katalog gespeichert, und der Mitarbeiter erscheint bei der Terminvergabe als möglicher Ansprechpartner für "Beratung"

##### Scenario: Bereits zugeordnete, inzwischen deaktivierte Leistung bleibt sichtbar
- **GIVEN** ein Mitarbeiter hat eine Leistung zugeordnet, die inzwischen im Katalog deaktiviert wurde
- **WHEN** der Admin das Mitarbeiterformular öffnet
- **THEN** bleibt die bestehende Zuordnung unverändert sichtbar (Snapshot-Name), ist aber nicht erneut aus der Auswahlliste neu zuordenbar

## 4. UI/UX Requirements
- **Komponenten**: `service-catalog-list.component.ts` (Tabelle, Suche, Status-Filter, nur für Admin erreichbar — neuer Navigationspunkt in `app.html`, sichtbar nur für `Admin`-Rolle), `service-catalog-form.component.ts` (Anlegen/Bearbeiten, DaisyUI `card`/`fieldset`, analog zu `customer-form`), Bestätigungsdialog (`modal`) für Deaktivierung.
- **Employee-Form-Änderung**: `employee-form.component.html` — Ersetze die beiden Freitextfelder (`serviceId`-Input, `serviceName`-Input) durch ein `<select>` (gespeist aus `GET /api/services?status=true`), das bei Auswahl automatisch `serviceId`+`serviceName` aus dem Katalog übernimmt; "Hinzufügen"-Button bleibt erhalten.
- **State**: Angular Signals/State-Service für Leistungsliste, Suchfilter, ausgewählte Leistung — analog zum bestehenden `customer-state.service.ts`-Muster.
- **i18n / a11y**: Neue DE/EN-Keys unter `catalog.*`; Formularfelder mit `aria-invalid`/`aria-describedby`; Tabelle mit `aria-sort`; Select im Mitarbeiterformular mit korrektem `<label>`.

## 5. API & Backend Requirements
- **Service:** `catalog-service` (eigenständig deploybar, eigene PostgreSQL-Instanz `catalog_db`, eigenes Aspire-Ressourcen-Modul, analog zu `customer-service`).
- **Authentifizierung/Autorisierung**: `[Authorize]` Standard; Rolle `Admin` für Anlegen/Bearbeiten/Deaktivieren; Rolle `Employee` darf lesen (für die Leistungsauswahl, falls Mitarbeiter künftig eigene Zuordnungen pflegen dürfen — aktuell nur Admin, siehe `mitarbeiterverwaltung.openspec.md` Abschnitt 5).
- **Endpoints**:
  - `GET /api/services?search=&status=&page=`: Paginierte, filterbare Leistungsliste
  - `GET /api/services/{id}`: Leistungsdetails
  - `POST /api/services`: Leistung anlegen
  - `PUT /api/services/{id}`: Leistung aktualisieren
  - `POST /api/services/{id}/deactivate`: Leistung deaktivieren (Rolle `Admin`)
- **Modelle/Entitäten (PostgreSQL, eigene DB `catalog_db`):**
  - `Service`: `Id (Guid)`, `Name (string)`, `Description (string?)`, `DefaultDurationMinutes (int?)`, `IsActive (bool)`, `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`
- **Inter-Service-Kommunikation:** Nur `crm-web` (Frontend) ruft `catalog-service` direkt über das Gateway auf, um die Auswahlliste im Mitarbeiterformular zu befüllen. Kein synchroner Backend-zu-Backend-Call von `employee-service`/`appointment-service` zu `catalog-service` (kein Gold-Plating — die bestehende Snapshot-Struktur in `EmployeeServiceAssignment` reicht aus, siehe Design).
- **Gateway:** neue Route `/api/services/**` → `catalog-service-cluster`, Auth-Termination wie bei den anderen drei Services.

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Nein.
- **Prompt/System Message**: Entfällt.

## 7. Design (Technischer Ansatz)
`catalog-service` als eigenständiger Microservice mit interner Clean-Architecture-Schichtung, exakt nach dem bereits etablierten Muster von `customer-service` (kleinste, einfachste bestehende Referenz: ein einziges Aggregate ohne Child-Entities). Domain-Layer: `Service` als Aggregate Root (Name, Description, DefaultDurationMinutes, IsActive), Domain Event `ServiceDeactivated` (optional, nur falls ein Konsument sinnvoll ist — aktuell kein Konsument, daher ohne Event-Publishing zulässig, kein Gold-Plating). Application-Layer: CQRS Commands (`CreateServiceCommand`, `UpdateServiceCommand`, `DeactivateServiceCommand`) und Queries (`SearchServicesQuery`, `GetServiceByIdQuery`) via Wolverine. Infrastructure: EF-Core-Repository gegen eigene PostgreSQL-Instanz `catalog_db`, automatische Migration beim Start (`DatabaseMigrationHostedService<TDbContext>`, bereits etabliertes Muster aus den anderen drei Services, siehe Bug 5 in `Tests/E2E/README.md`). Web: Controller mit JWT-Validierung (gleiches Issuer/Audience/SigningKey-Schema wie die anderen Services), CORS (`AllowedOrigins: ["http://localhost:4200"]`), zentrales Exception Handling, Swagger/OpenAPI (analog den anderen vier Services). `employee-form.component.ts` in `crm-web` ruft `catalog-service` direkt über das Gateway auf (neuer, schlanker `service-catalog-api.service.ts` im `employees`-Feature-Bereich, kein Cross-Import). AppHost: `catalog-postgres`/`catalog-db` (Connection-Name `catalog_db`) registrieren, `catalog-service`-Projektressource mit demselben JWT-Parameter-Satz wie die anderen Services, Gateway-Referenz ergänzen.

## 8. Tasks (Implementierungs-Checkliste)
- [x] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/CatalogService.Tests`
- [x] 2. Backend implementieren (Domain → Application → Infrastructure → Web) – `Services/catalog-service/`
- [x] 3. Frontend implementieren: neue `service-catalog-list`/`service-catalog-form`-Komponenten (Admin-Bereich) + Umstellung der Leistungszuordnung in `employee-form.component.ts` auf Auswahl aus dem Katalog
- [x] 4. E2E-Tests (QA) grün
- [x] 5. Doku/OpenAPI aktualisiert (doc-agent, 2026-10-30: `Services/catalog-service/README.md` neu, Root-`README.md`/`AppHost/README.md` um `catalog-service` ergänzt; Swagger/XML-Doc-Kommentare in `CatalogService.Web` waren bereits konform zum etablierten Muster)
- [x] 6. Aspire AppHost-Ressource für `catalog-service` + `catalog_db` registrieren
- [x] 7. YARP-Gateway-Route `/api/services/**` inkl. Auth-Termination konfigurieren, Gateway-CORS/Origin-Liste prüfen (kein neuer Origin nötig, `crm-web` bleibt 4200)
- [x] 8. `UserStories/specs/employee-management.openspec.md` (Requirement "Leistungen/Qualifikationen zuordnen" + Architektur-Verweise) aktualisiert; `UserStories/archive/2026-10-01-mitarbeiterverwaltung.openspec.md` mit Nachtrags-Hinweis versehen (po-agent, 2026-10-30)

## 9. Acceptance Criteria (DoD)
- [x] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün.
- [x] Backend API (C# 14) ist implementiert und getestet (TUnit).
- [x] Angular-Komponenten sind implementiert und gestylt (DaisyUI 5).
- [x] E2E Tests (QA Agent) sind grün.
- [x] Swagger/OpenAPI Compliance ist gegeben.
- [ ] Spec ist archiviert (Status `archived`, Requirements ggf. in `UserStories/specs/service-catalog.openspec.md` übernommen). — ausstehend, Aufgabe des `po-agent`.
