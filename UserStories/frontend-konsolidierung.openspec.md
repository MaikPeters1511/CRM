# Change: Frontend-Konsolidierung (ein gemeinsames Frontend)

> **Status:** proposed
> Ablage: `UserStories/frontend-konsolidierung.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-frontend-konsolidierung.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Aktuell existieren drei getrennte Angular-Anwendungen (`customer-web`, `employee-web`, `appointment-web`), jede mit eigenem Login-Flow, eigenem Dev-Port, eigenem Deployment und eigener i18n/Auth-Boilerplate. Das erzeugt unnötigen Pflegeaufwand (dreifacher Login/Interceptor/Guard-Code, wie auch vom `qa-agent` als CORS-Risiko pro App identifiziert) und einen für Endnutzer unnötig fragmentierten Arbeitsablauf (Wechsel zwischen Kunde/Mitarbeiter/Termin erfordert App-Wechsel). Der Nutzer möchte statt drei Apps ein einziges Frontend.
- **What Changes:**
  - Zusammenführen von `customer-web`, `employee-web` und `appointment-web` zu einer neuen, einzigen Angular-Anwendung `Clients/crm-web` mit gemeinsamer Shell (Navigation, Login, Auth-Session) und lazy-geladenen Feature-Bereichen `customers`, `employees`, `appointments`.
  - Migration der bestehenden, bereits fertigen Komponenten (Kunden-/Mitarbeiter-/Termin-UI) unverändert in fachlicher Funktion — reine strukturelle Konsolidierung, **keine** Verhaltensänderung der bisherigen Scenarios.
  - Entfernen der drei bisherigen eigenständigen Angular-Projekte nach erfolgreicher Migration.
  - Anpassung der E2E-Tests (`Tests/E2E/`) auf eine einzige Basis-URL/Port statt drei.
  - Reduktion der Gateway-CORS-Origin-Liste auf den einen neuen Frontend-Origin.
- **Impact:** Betrifft ausschließlich die Presentation-Schicht. Die drei Backend-Microservices (`customer-service`, `employee-service`, `appointment-service`) und `identity-service` bleiben unverändert getrennt (Database-per-Service, ADR `docs/adr/0001-microservices-architektur-crm.md` bleibt für das Backend unverändert gültig). Diese Konsolidierung ist eine bewusste Ausnahme zur bisherigen "ein Client pro Service"-Praxis auf Frontend-Seite; dokumentiert in `docs/adr/0002-frontend-konsolidierung.md` (verweist auf und präzisiert ADR 0001 ausschließlich hinsichtlich der Frontend-Topologie). Ersetzt/modifiziert Abschnitt 4 (UI/UX Requirements) in `kundenverwaltung.openspec.md`, `mitarbeiterverwaltung.openspec.md` und `terminverwaltung.openspec.md` dahingehend, dass die dort genannten Komponenten künftig Teil von `Clients/crm-web` statt eigenständiger Angular-Projekte sind — die dortigen Requirements/Scenarios (Abschnitt 3) bleiben inhaltlich unverändert gültig und müssen nach der Migration weiterhin grün sein.

## 2. User Stories
- Als Nutzer (Admin oder Employee) möchte ich mich einmal einloggen und danach nahtlos zwischen Kunden-, Mitarbeiter- und Terminverwaltung wechseln, ohne mehrere Apps/URLs/Logins zu benötigen.
- Als Nutzer möchte ich eine gemeinsame Navigation sehen, über die ich alle drei Bereiche erreiche.
- Als Entwickler/Betreiber möchte ich nur noch ein Frontend-Deployment und einen Frontend-Origin pflegen statt drei.

## 3. Requirements (Delta)

### ADDED Requirements

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
- **THEN** zeigt die Navigation Einträge für Kunden, Mitarbeiter und Termine, jeweils führend zum entsprechenden Feature-Bereich

#### Requirement: Regressionsfreie Migration bestehender Funktionalität
Das System SHALL nach der Konsolidierung alle bereits spezifizierten Scenarios aus `kundenverwaltung.openspec.md`, `mitarbeiterverwaltung.openspec.md` und `terminverwaltung.openspec.md` (jeweils Abschnitt 3) unverändert erfüllen.

##### Scenario: Bestehende Scenarios bleiben gültig
- **GIVEN** alle Scenarios aus den drei genannten Specs, vormals gegen `customer-web`/`employee-web`/`appointment-web` getestet
- **WHEN** sie gegen die konsolidierte Anwendung `crm-web` ausgeführt werden
- **THEN** verhalten sie sich identisch zu den bisherigen Einzel-Apps (keine fachliche Regression)

## 4. UI/UX Requirements
- **Struktur**: Eine Angular-22-Standalone-Anwendung `Clients/crm-web` mit Shell-Komponente (Header/Navigation/Router-Outlet) und drei Lazy-Routes: `/customers/**`, `/employees/**`, `/appointments/**`. Bestehende Komponenten (`customer-list/-form/-detail`, `employee-list/-form/-schedule`, `absence-form`, `calendar-view`, `appointment-form/-detail`) werden 1:1 aus den drei bisherigen Apps übernommen (Verzeichnisstruktur je Feature-Bereich beibehalten: `customers/domain|infrastructure|application|presentation`, analog für `employees`/`appointments`).
- **Auth**: Ein einziger Login-Flow, ein `AuthStateService`, ein `authInterceptor`, ein `authGuard` für die gesamte App (aus den drei bisherigen, identischen Implementierungen zusammengeführt — keine Neuentwicklung).
- **i18n / a11y**: Zusammenführen der drei bisherigen i18n-Dateien (`customer.*`, `employee.*`, `appointment.*`, `auth.*`) in eine gemeinsame `public/i18n/de.json`/`en.json`; bestehende a11y-Eigenschaften (ARIA-Rollen, Tastaturnavigation) bleiben unverändert erhalten.

## 5. API & Backend Requirements
- Keine Backend-Änderung. `crm-web` spricht weiterhin ausschließlich über das YARP-Gateway (`Gateway/Crm.Gateway/`) mit `identity-service`, `customer-service`, `employee-service`, `appointment-service` — Routen (`/api/auth/**`, `/api/customers/**`, `/api/employees/**`, `/api/appointments/**`) bleiben unverändert.
- **Gateway-CORS**: `Cors:AllowedOrigins` in `Gateway/Crm.Gateway/appsettings.json` (und Fallback in `Program.cs`) wird von drei Frontend-Origins (4200/4201/4202) auf den einen neuen `crm-web`-Dev-Origin reduziert.

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Nein.
- **Prompt/System Message**: Entfällt.

## 7. Design (Technischer Ansatz)
Neue eigenständige Angular-Anwendung `Clients/crm-web`, die die drei bisherigen Apps ablöst. Migration erfolgt komponentenweise (Verzeichnisse aus `customer-web`/`employee-web`/`appointment-web` in entsprechende Feature-Ordner von `crm-web` verschieben, Imports/Routing anpassen), nicht als Neuentwicklung — Ziel ist strukturelle Konsolidierung ohne Verhaltensänderung. Auth-Infrastruktur (State-Service, Interceptor, Guard) existiert in allen drei bisherigen Apps bereits identisch und wird einmalig übernommen. Nach erfolgreichem, grünem Migrations-Ergebnis (alle bestehenden Tests weiterhin grün, plus neue Shell-/Navigations-Tests) werden `Clients/customer-web`, `Clients/employee-web`, `Clients/appointment-web` sowie die zugehörigen Testordner `Tests/CustomerWeb.Tests`, `Tests/EmployeeWeb.Tests`, `Tests/AppointmentWeb.Tests` entfernt und durch `Tests/CrmWeb.Tests` ersetzt (Testinhalte werden migriert, nicht verworfen). `Tests/E2E/` wird auf eine einzige Basis-URL umgestellt. Diese Konsolidierung ist eine Presentation-Layer-Entscheidung und weicht bewusst von der bisherigen "ein Client pro Microservice"-Praxis ab; `architect-agent` dokumentiert diese Abweichung als Ergänzung zu `docs/adr/0001-microservices-architektur-crm.md`, bevor die Migration beginnt (Begründung: geteilte UX/Login-Konsistenz überwiegt die Entkopplung auf Frontend-Seite; die Backend-Entkopplung bleibt vollständig erhalten).

## 8. Tasks (Implementierungs-Checkliste)
- [x] 1. ADR-Ergänzung durch `architect-agent`: Frontend-Konsolidierung als Ausnahme zur Client-pro-Service-Praxis dokumentiert (`docs/adr/0002-frontend-konsolidierung.md`)
- [ ] 2. Tests aus den neuen Scenarios (Shell/Navigation ohne Re-Login, rollenbasierte Navigation) schreiben (Red) – `Tests/CrmWeb.Tests/`
- [ ] 3. Neue App `Clients/crm-web` anlegen (Shell, Routing, gemeinsamer Auth-Flow, zusammengeführte i18n)
- [ ] 4. Feature-Bereiche `customers`, `employees`, `appointments` aus den drei bisherigen Apps migrieren (Komponenten, State, API-Clients unverändert übernehmen)
- [ ] 5. Bestehende Tests aus `Tests/CustomerWeb.Tests`, `Tests/EmployeeWeb.Tests`, `Tests/AppointmentWeb.Tests` nach `Tests/CrmWeb.Tests` migrieren, alle grün
- [ ] 6. Gateway-CORS auf einen Frontend-Origin reduzieren (`Gateway/Crm.Gateway/appsettings.json` + `Program.cs`-Fallback)
- [ ] 7. `Tests/E2E/` auf einzige Basis-URL umstellen, alle 30 bestehenden E2E-Tests weiterhin ausführbar
- [ ] 8. Alte Projekte entfernen: `Clients/customer-web`, `Clients/employee-web`, `Clients/appointment-web`
- [ ] 9. Doku aktualisieren (`AppHost/README.md`, ggf. weitere READMEs mit Verweis auf die drei alten Ports)

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 dieser Spec sowie alle bisherigen Scenarios aus `kundenverwaltung.openspec.md`, `mitarbeiterverwaltung.openspec.md`, `terminverwaltung.openspec.md` (Abschnitt 3) sind gegen `crm-web` durch automatisierte Tests abgedeckt und grün.
- [ ] `ng build` für `crm-web` läuft fehlerfrei.
- [ ] E2E-Tests (`Tests/E2E/`) laufen gegen die konsolidierte App grün.
- [ ] Die drei alten Angular-Projekte sind entfernt, keine toten Referenzen (Gateway-CORS, AppHost, Doku) verbleiben.
- [ ] `architect-agent` hat die ADR-Ergänzung zu ADR 0001 dokumentiert.
- [ ] Spec ist archiviert (Status `archived`, Requirements ggf. in `UserStories/specs/frontend-shell.openspec.md` übernommen).
