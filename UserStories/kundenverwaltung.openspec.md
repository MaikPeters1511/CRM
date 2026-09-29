# Change: Kundenverwaltung

> **Status:** in-progress
> Ablage: `UserStories/kundenverwaltung.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-kundenverwaltung.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Das CRM benötigt eine zentrale Verwaltung von Kundendaten als Grundlage für Terminvergabe, Kommunikation und Reporting. Ohne strukturierte Kundenverwaltung sind Termine und Mitarbeiterzuordnungen nicht sinnvoll abbildbar.
- **What Changes:**
  - Neue Capability `Customers` (Anlegen, Anzeigen, Bearbeiten, Deaktivieren/Löschen von Kunden).
  - Suche/Filterung von Kunden.
  - Kundendetailsicht inkl. Historie (Termine referenzieren später den Kunden).
- **Impact:** Neuer eigenständiger Microservice `customer-service` (Bounded Context "Customer Management") mit eigener Datenbank (PostgreSQL, Schema/DB pro Service). Wird vom `appointment-service` (Change `terminverwaltung.openspec.md`) per synchronem API-Call oder Integration Event referenziert (Kunde ⇄ Termin) – kein direkter DB-Zugriff über Servicegrenzen hinweg. Architekturentscheidung "Microservices statt Modularer Monolith" (Service-Grenzen, Kommunikationsmuster, Datenkonsistenz) ist dokumentiert in `docs/adr/0001-microservices-architektur-crm.md`.

## 2. User Stories
- Als Mitarbeiter möchte ich einen neuen Kunden mit Stammdaten (Name, Kontakt, Adresse) anlegen, damit ich ihn später Terminen zuordnen kann.
- Als Mitarbeiter möchte ich Kunden nach Namen/Telefonnummer/E-Mail suchen, damit ich schnell den richtigen Datensatz finde.
- Als Mitarbeiter möchte ich Kundendaten bearbeiten, damit veraltete Informationen aktualisiert werden können.
- Als Admin möchte ich einen Kunden deaktivieren (Soft-Delete), damit inaktive Kunden nicht mehr in der aktiven Auswahl erscheinen, die Historie aber erhalten bleibt.
- Als Mitarbeiter möchte ich in der Kundendetailsicht alle vergangenen und zukünftigen Termine des Kunden sehen, damit ich einen vollständigen Überblick habe.

## 3. Requirements (Delta)

### ADDED Requirements

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

## 4. UI/UX Requirements
- **Komponenten**: `customer-list.component.ts` (Tabelle mit DaisyUI `table`, Suchfeld, Filter-Dropdown), `customer-form.component.ts` (DaisyUI `input`/`select`, Standalone), `customer-detail.component.ts` (Tabs: Stammdaten/Termine), Bestätigungsdialog (DaisyUI `modal`) für Deaktivierung.
- **State**: Angular Signals/State-Service für aktuelle Kundenliste, Suchfilter, ausgewählten Kunden; optimistic UI bei Speichern optional, aber Fehler-Rollback erforderlich.
- **i18n / a11y**: Alle Labels/Fehlermeldungen über DE/EN i18n-Keys (`customer.*`); Formularfelder mit `aria-label`/`aria-describedby` für Validierungsfehler; Tabelle mit `aria-sort` bei Sortierung; vollständige Tastaturnavigation (Tab-Reihenfolge, Enter zum Speichern, Esc zum Schließen von Modals).

## 5. API & Backend Requirements
- **Service:** `customer-service` (eigenständig deploybar, eigene PostgreSQL-Instanz/Schema, eigenes Aspire-Ressourcen-Modul).
- **Authentifizierung/Autorisierung**: `[Authorize]` Standard; Rollen `Employee` (Lesen/Anlegen/Bearbeiten), `Admin` (zusätzlich Deaktivieren) via Microsoft Identity Claims (Token-Validierung im Service, zentrale Identity-Instanz/API-Gateway).
- **Endpoints**:
  - `GET /api/customers?search=&status=&page=`: Paginierte, filterbare Kundenliste
  - `GET /api/customers/{id}`: Kundendetails inkl. Terminreferenzen (Termine werden per Call an `appointment-service` nachgeladen, nicht per DB-Join)
  - `POST /api/customers`: Kunde anlegen
  - `PUT /api/customers/{id}`: Kunde aktualisieren
  - `POST /api/customers/{id}/deactivate`: Kunde deaktivieren (Rolle `Admin`) → publiziert Integration Event `CustomerDeactivated` (z.B. via Message Broker/Wolverine Messaging) für konsumierende Services
- **Modelle/Entitäten (PostgreSQL, eigene DB `customer_db`):**
  - `Customer`: `Id (Guid)`, `FirstName (string)`, `LastName (string)`, `Email (string?)`, `Phone (string?)`, `Address (ValueObject: Street, City, ZipCode, Country)`, `IsActive (bool)`, `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`
- **Inter-Service-Kommunikation:** `appointment-service` fragt Kundendaten synchron via `GET /api/customers/{id}` ab (HTTP/Service Discovery über .NET Aspire), siehe `docs/adr/0001-microservices-architektur-crm.md`.

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Nein (Basis-Feature; spätere Erweiterung um semantische Suche möglich).
- **Prompt/System Message**: Entfällt.

## 7. Design (Technischer Ansatz)
`customer-service` als eigenständiger Microservice mit Clean-Architecture-internen Layern: Domain-Layer: `Customer` als Aggregate Root mit `Address` als Value Object, Domain Event `CustomerDeactivated`. Application-Layer: CQRS Commands (`CreateCustomerCommand`, `UpdateCustomerCommand`, `DeactivateCustomerCommand`) und Queries (`SearchCustomersQuery`, `GetCustomerByIdQuery`) via Wolverine. Infrastructure: EF Core Repository gegen eigene PostgreSQL-Instanz `customer_db`, `.Include()` nur innerhalb des Service (keine Cross-Service-Joins). Terminreferenzen werden über Inter-Service-Call/Event, nicht per DB-Join, aufgelöst. Service wird über .NET Aspire AppHost registriert und über API-Gateway/Service Discovery erreichbar gemacht. Die grundsätzliche Microservices-Zerlegung (Servicegrenzen, Kommunikationsmuster synchron vs. Event-basiert, Datenkonsistenzstrategie zwischen `customer-service`, `employee-service` und `appointment-service`) ist als ADR in `docs/adr/0001-microservices-architektur-crm.md` festgehalten.

## 8. Tasks (Implementierungs-Checkliste)
- [x] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/`
- [x] 2. Backend implementieren (Domain → Application → Infrastructure → Web) — mit Einschränkung: `GET /api/customers/{id}` liefert noch keine Terminreferenzen aus `appointment-service` (dieser Service existiert im Projekt noch nicht, siehe Bericht des backend-agent)
- [x] 3. Frontend implementieren (Angular 22, DaisyUI 5, i18n, ARIA) — Standalone-App `Clients/customer-web` mit `customer-list`/`customer-form`/`customer-detail`-Komponenten, State-Service (Signals, Fehler-Rollback), i18n (DE/EN via ngx-translate, Keys `customer.*`), ARIA (aria-sort, aria-invalid/-describedby, Modal mit Esc-Handling); Terminhistorie im Detail-Tab zeigt bewusst einen Hinweis "noch nicht verfügbar", da `GET /api/customers/{id}` aktuell keine Terminreferenzen liefert (siehe Task 2)
- [ ] 4. E2E-/Compliance-Tests (QA) grün
- [ ] 5. Doku/OpenAPI aktualisiert — Swagger/OpenAPI ist über Swashbuckle im Web-Projekt aktiv (`/swagger`), separate Doku-Artefakte durch doc-agent stehen noch aus
- [x] 6. Aspire AppHost-Ressource für `customer-service` + `customer_db` gemäß ADR 0001 registrieren (devops-agent) — `AppHost/Crm.AppHost/AppHost.cs`: `AddPostgres("customer-postgres").AddDatabase("customer-db", databaseName: "customer_db")`, referenziert von `customer-service` via `WithReference(customerDb, connectionName: "customer_db")`; End-to-End lokal mit Docker verifiziert (Container startet, Service erreicht Zustand `Running`)
- [x] 7. Wolverine-Event-Schema für `CustomerDeactivated` definieren und im Message Broker (RabbitMQ) publizieren (backend-agent, ADR 0001) — Contract `CustomerDeactivatedIntegrationEvent` implementiert, Publish via Wolverine `IMessageBus`; RabbitMQ-Transport ist konfigurierbar über Connection String (aktiv sobald Broker via Aspire/Docker bereitgestellt wird)
- [x] 8. YARP-Gateway-Route für `customer-service` inkl. Auth-Termination konfigurieren (devops-agent/sec-agent, ADR 0001) — `Gateway/Crm.Gateway`: Route `/api/customers/{**catch-all}` → Cluster `customer-service-cluster` (Ziel via Aspire Service Discovery, `http://customer-service`), JWT-Auth-Termination (`Authentication:JwtBearer`, Policy `gateway-authenticated`); Authority/Audience-Feinabstimmung mit sec-agent noch offen (Authority aktuell leer, analog zu den Services)

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün.
- [ ] Backend API (C# 14) ist implementiert und getestet (TUnit).
- [ ] Angular 22 Komponente ist implementiert und gestylt (DaisyUI 5).
- [ ] E2E Tests (QA Agent) sind grün.
- [ ] Swagger/OpenAPI Compliance ist gegeben.
- [ ] Spec ist archiviert (Status `archived`, Requirements ggf. in `UserStories/specs/customer-management.openspec.md` übernommen).
