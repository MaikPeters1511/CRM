# Change: Abwesenheits-Freigabe-Workflow

> **Status:** proposed
> Ablage: `UserStories/abwesenheit-workflow.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-abwesenheit-workflow.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Abwesenheiten (`POST /api/employees/{id}/absences`) werden heute sofort als verbindlich gespeichert, ohne Freigabe durch einen Admin/Teamleiter. Der Mitarbeiter erhält keine Rückmeldung über den Status, und genehmigte Abwesenheiten erscheinen nicht im Terminkalender. Für einen echten HR-Workflow braucht es einen Genehmigungsstatus, eine Admin-Freigabe-Ansicht, eine Benachrichtigung bei Entscheidung und eine sichtbare Kennzeichnung im Kalender.
- **What Changes:**
  - `Absence` erhält einen Status (`Pending`/`Approved`/`Rejected`), Einreicher, Freigeber/Ablehner und Zeitstempel.
  - Neue Endpunkte zum Genehmigen/Ablehnen ausstehender Anfragen (Rolle `Admin`).
  - Neue, einfache Notification-Entität in `employee-service` mit Endpunkten zum Abrufen/Als-gelesen-markieren.
  - `appointment-service` fragt bei der Kalenderabfrage zusätzlich genehmigte Abwesenheiten bei `employee-service` ab und rendert sie als separate Ganztags-Einträge.
  - Frontend: Status-Anzeige für eigene Anträge, Admin-Freigabe-Ansicht, Glocken-Komponente mit echten Daten, Kalender-Integration.
- **Impact:** Erweiterung von `employee-service` (bestehende `Absence`-Entity, neue `Notification`-Entity, neue EF-Migration). `appointment-service` bekommt einen neuen synchronen Outbound-Call zu `employee-service` (analog zum bestehenden Muster `IAppointmentAvailabilityClient`, nur umgekehrte Richtung). Kein neuer Microservice. Frontend-Änderungen bleiben im Feature-Bereich `employees` und `appointments` von `crm-web`; die Glocke wird in der App-Shell (`app.html`/`app.ts`) ergänzt, unabhängig vom Stand der Sidebar-Umstellung in PR #1.

## 2. User Stories
- Als Mitarbeiter möchte ich eine Abwesenheit beantragen und den Status (ausstehend/genehmigt/abgelehnt) meiner Anträge sehen.
- Als Admin/Teamleiter möchte ich ausstehende Abwesenheitsanfragen einsehen und genehmigen oder ablehnen.
- Als Mitarbeiter möchte ich benachrichtigt werden (Glocke), wenn über meinen Antrag entschieden wurde.
- Als Nutzer möchte ich genehmigte Abwesenheiten im Terminkalender als "Abwesend" sehen, um Teamverfügbarkeit einzuschätzen.

## 3. Requirements (Delta)

### MODIFIED Requirements

#### Requirement: Abwesenheiten erfassen
Das System SHALL das Erfassen von Abwesenheiten (Urlaub, Krankheit, Sonstige) mit Zeitraum ermöglichen. Eine neu erfasste Abwesenheit SHALL im Status `Pending` angelegt werden und erst nach Freigabe durch einen Admin für die Terminverfügbarkeit und Kalenderanzeige wirksam werden.

##### Scenario: Urlaub eintragen (Status Pending)
- **GIVEN** ein Mitarbeiter hat für einen Zeitraum bereits gebuchte Termine außerhalb des geplanten Urlaubs
- **WHEN** er einen Urlaub für einen termin-freien Zeitraum einträgt
- **THEN** wird die Abwesenheit mit Status `Pending` gespeichert und ist für Admins in der Freigabe-Ansicht sichtbar

##### Scenario: Urlaub kollidiert mit bestehendem Termin
- **GIVEN** ein Mitarbeiter hat einen bestätigten Termin am 15.10.
- **WHEN** er versucht, für den 15.10. eine Abwesenheit einzutragen
- **THEN** wird eine Konfliktwarnung angezeigt und die Bestätigung erfordert eine explizite Bestätigung oder Terminverschiebung

### ADDED Requirements

#### Requirement: Abwesenheitsanfrage genehmigen oder ablehnen
Das System SHALL es einem Admin ermöglichen, eine ausstehende Abwesenheitsanfrage zu genehmigen oder abzulehnen. Nur Admins SHALL diese Aktion ausführen dürfen.

##### Scenario: Admin genehmigt eine ausstehende Anfrage
- **GIVEN** eine Abwesenheitsanfrage eines Mitarbeiters mit Status `Pending`
- **WHEN** ein Admin die Anfrage genehmigt
- **THEN** wird der Status auf `Approved` gesetzt, Freigeber und Zeitpunkt werden gespeichert, eine Benachrichtigung für den Mitarbeiter wird angelegt, und der Zeitraum erscheint im Terminkalender als "Abwesend"

##### Scenario: Admin lehnt eine ausstehende Anfrage ab
- **GIVEN** eine Abwesenheitsanfrage eines Mitarbeiters mit Status `Pending`
- **WHEN** ein Admin die Anfrage ablehnt
- **THEN** wird der Status auf `Rejected` gesetzt, eine Benachrichtigung für den Mitarbeiter wird angelegt, und der Zeitraum erscheint nicht im Kalender

##### Scenario: Mitarbeiter versucht eine Anfrage zu genehmigen
- **GIVEN** ein authentifizierter Mitarbeiter ohne Admin-Rolle
- **WHEN** er versucht, eine Abwesenheitsanfrage über den Genehmigungs-Endpunkt zu genehmigen
- **THEN** antwortet das System mit `403 Forbidden`

##### Scenario: Bereits entschiedene Anfrage erneut entscheiden
- **GIVEN** eine Abwesenheitsanfrage mit Status `Approved` oder `Rejected`
- **WHEN** ein Admin erneut versucht, sie zu genehmigen oder abzulehnen
- **THEN** antwortet das System mit einem Konfliktfehler, der Status bleibt unverändert

#### Requirement: Eigene Abwesenheitsanfragen mit Status einsehen
Das System SHALL einem Mitarbeiter die Einsicht in alle eigenen Abwesenheitsanfragen inklusive Status ermöglichen.

##### Scenario: Mitarbeiter sieht eigene Anträge
- **GIVEN** ein Mitarbeiter hat zwei Abwesenheitsanfragen mit unterschiedlichem Status eingereicht
- **WHEN** er seine Abwesenheitsübersicht öffnet
- **THEN** sieht er beide Anfragen mit jeweiligem Status (`Pending`/`Approved`/`Rejected`)

#### Requirement: Benachrichtigung bei Entscheidung über Abwesenheitsanfrage
Das System SHALL dem einreichenden Mitarbeiter eine Benachrichtigung anlegen, wenn über seine Abwesenheitsanfrage entschieden wurde. Benachrichtigungen SHALL abrufbar und als gelesen markierbar sein.

##### Scenario: Benachrichtigung nach Genehmigung
- **GIVEN** eine Abwesenheitsanfrage eines Mitarbeiters wurde soeben von einem Admin genehmigt
- **WHEN** der Mitarbeiter seine Benachrichtigungen abruft
- **THEN** enthält die Liste eine ungelesene Benachrichtigung über die Genehmigung mit Link zur Anfrage

##### Scenario: Benachrichtigung als gelesen markieren
- **GIVEN** eine ungelesene Benachrichtigung existiert für einen Mitarbeiter
- **WHEN** der Mitarbeiter sie als gelesen markiert
- **THEN** zählt sie nicht mehr zum ungelesen-Zähler

#### Requirement: Genehmigte Abwesenheiten im Terminkalender anzeigen
Das System SHALL genehmigte Abwesenheiten im Terminkalender als separate, visuell abgesetzte Ganztags-Einträge je betroffenem Mitarbeiter anzeigen. Ausstehende oder abgelehnte Abwesenheiten SHALL NOT im Kalender erscheinen.

##### Scenario: Genehmigte Abwesenheit erscheint im Kalender
- **GIVEN** eine Abwesenheit vom 20.–22.11. wurde für einen Mitarbeiter genehmigt
- **WHEN** ein Nutzer den Kalender für diesen Zeitraum öffnet
- **THEN** werden die Tage 20.–22.11. für diesen Mitarbeiter als "Abwesend"-Ganztageseintrag angezeigt, optisch getrennt von regulären Terminen

## 4. UI/UX Requirements
- **Komponenten:** `absence-form.component.ts` (bestehend, erweitert um Statusanzeige der eigenen Anträge als Liste/Badges unterhalb des Formulars), neue `absence-approvals.component.ts` (Admin-Ansicht: Liste ausstehender Anfragen mit Mitarbeitername, Zeitraum, Typ, Genehmigen-/Ablehnen-Buttons), neue `notification-bell.component.ts` in der App-Shell (Dropdown, ungelesen-Zähler-Badge, Liste, "als gelesen markieren").
- **Kalender:** `calendar-view.component.ts` rendert genehmigte Abwesenheiten als zusätzliche Ganztages-Balken mit eigener DaisyUI-Badge-Farbe (`badge-warning` o.ä.), getrennt von Termin-Einträgen; Tooltip mit Mitarbeitername + Abwesenheitstyp.
- **State:** Neuer `absence-state.service.ts` (eigene Anträge + Admin-Pending-Liste), neuer `notification-state.service.ts` (Polling oder Lazy-Load beim Öffnen der Glocke, Signal für ungelesen-Zähler).
- **i18n / a11y:** Neue Keys unter `absence.*`/`notification.*` (DE/EN). Freigabe-Buttons mit `aria-label`; Glocke als `<button aria-haspopup="true" aria-label="…">` mit `aria-live="polite"`-Region für den Zähler; Statusbadges mit Text (nicht nur Farbe) für Farbfehlsichtige.

## 5. API & Backend Requirements
- **Service:** `employee-service` (bestehend), Erweiterung der `EmployeeManagement`-Domäne; kein neuer Microservice.
- **Authentifizierung/Autorisierung:** `[Authorize]` Standard. `POST .../absences` und `GET .../absences` (eigene) wie bisher: Rolle `Employee`/`Admin`, Ownership-Check bei `Employee`. `POST .../absences/{absenceId}/approve` und `.../reject`: ausschließlich Rolle `Admin`. `GET /api/notifications` und `POST /api/notifications/{id}/read`: eigene Benachrichtigungen, Ownership-Check analog Abwesenheiten.
- **Endpoints (neu/geändert):**
  - `POST /api/employees/{id}/absences` (bestehend, Response jetzt inkl. `Status=Pending`, `RequestedBy`, `RequestedAt`)
  - `GET /api/employees/{id}/absences`: eigene Anfragen inkl. Status (Mitarbeiter: nur eigene; Admin: beliebige)
  - `GET /api/employees/absences/pending`: alle ausstehenden Anfragen (Rolle `Admin`)
  - `POST /api/employees/{id}/absences/{absenceId}/approve`: genehmigt (Rolle `Admin`) → publiziert `AbsenceApproved`
  - `POST /api/employees/{id}/absences/{absenceId}/reject`: lehnt ab (Rolle `Admin`) → publiziert `AbsenceRejected`
  - `GET /api/employees/absences/approved?from=&to=&employeeId=`: interner Lesezugriff für `appointment-service` (Rolle `Admin`-Service-Token/`Employee,Admin`, analog bestehendem Muster), liefert genehmigte Abwesenheiten im Zeitraum
  - `GET /api/notifications`: eigene Benachrichtigungen (neueste zuerst)
  - `POST /api/notifications/{id}/read`: markiert eine Benachrichtigung als gelesen
- **Modelle/Entitäten (PostgreSQL, `employee_db`):**
  - `Absence` (erweitert): `+ Status (enum: Pending, Approved, Rejected)`, `+ RequestedBy (string)`, `+ RequestedAt (DateTimeOffset)`, `+ DecidedBy (string?)`, `+ DecidedAt (DateTimeOffset?)`
  - `Notification` (neu, eigenständiges Aggregate): `Id (Guid)`, `RecipientEmployeeId (Guid)`, `Text (string)`, `Link (string?)`, `IsRead (bool)`, `CreatedAt (DateTimeOffset)`
- **Integration Events (Wolverine/RabbitMQ, analog `EmployeeDeactivated`):** `AbsenceApproved(AbsenceId, EmployeeId, From, To, Type, ApprovedBy, ApprovedAt)`, `AbsenceRejected(AbsenceId, EmployeeId, RejectedBy, RejectedAt)`. Beide werden innerhalb von `employee-service` selbst konsumiert, um die `Notification` anzulegen (In-Process-Handler, kein Transport nötig) — das Event wird dennoch als Integration Event publiziert, damit zukünftige Konsumenten (z.B. `appointment-service`) ohne Schema-Änderung andocken können.
- **Design-Entscheidung Kalender-Integration:** `appointment-service` fragt bei jeder Kalenderabfrage (`GetCalendarQuery`) zusätzlich synchron `GET /api/employees/absences/approved?from=&to=` bei `employee-service` ab (neuer `IEmployeeAbsenceClient`, HTTP + Polly-Resilienz, exakt analog zum bestehenden `IAppointmentAvailabilityClient` in `employee-service`, nur Richtung umgekehrt) und rendert die Treffer als zusätzliche, nicht-persistierte Ganztages-Einträge in der Kalenderantwort. **Begründung:** Vermeidet Datenduplizierung/Konsistenzprobleme einer Event-getriebenen Kopie der Abwesenheiten in `appointment-service` (Bounded-Context-Grenze: Abwesenheiten bleiben alleinige Verantwortung von `employee-service`); der Lesepfad ist unkritisch für Schreib-Performance und folgt dem bereits etablierten synchronen Inter-Service-Query-Muster aus ADR 0001.

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant):** Nein.
- **Prompt/System Message:** Entfällt.

## 7. Design (Technischer Ansatz)
`Absence` (Domain-Layer) erhält einen `AbsenceStatus`-Enum und `Approve(decidedBy, decidedAt)`/`Reject(decidedBy, decidedAt)`-Methoden mit Guard (`InvalidOperationException`/Result-Fehler bei bereits entschiedenem Status), die ein `AbsenceDecided`-Domain-Event auslösen. Application-Layer: neue Commands `ApproveAbsenceCommand`/`RejectAbsenceCommand`, Queries `GetPendingAbsencesQuery`/`GetApprovedAbsencesQuery`/`GetEmployeeAbsencesQuery`. Der Handler für Approve/Reject lässt bei Erfolg zusätzlich eine `Notification` anlegen (direkter Repository-Call, da gleicher Bounded Context — kein Messaging nötig für den In-Process-Teil) und publiziert das Integration Event über `IMessageBus`. `Notification` ist ein eigenes, einfaches Aggregate (kein Teil des `Employee`-Aggregats, da unabhängiger Lebenszyklus) mit eigenem EF-Core-Mapping/Repository. Neue EF-Migration für `Absence`-Spaltenerweiterung + `Notifications`-Tabelle. `appointment-service` erhält `IEmployeeAbsenceClient`/`EmployeeAbsenceHttpClient` (Infrastructure) analog zum bestehenden Pattern; `GetCalendarQuery`-Handler kombiniert Termine und Abwesenheiten in der Response, ohne sie zu persistieren.

## 8. Tasks (Implementierungs-Checkliste)
- [ ] 1. Tests aus den Scenarios schreiben (Red) – `Tests/EmployeeService.Tests`, `Tests/AppointmentService.Tests`, `Tests/CrmWeb.Tests`
- [ ] 2. Backend `employee-service`: `Absence`-Status/Approve/Reject, `Notification`-Aggregate, Endpunkte, EF-Migration, Integration Events (backend-agent)
- [ ] 3. Backend `appointment-service`: `IEmployeeAbsenceClient` + Kalender-Integration (backend-agent)
- [ ] 4. Frontend: Statusanzeige eigener Anträge, Freigabe-Ansicht, Glocke, Kalender-Integration (frontend-agent)
- [ ] 5. Lokale Verifikation: Backend-Tests (TUnit) grün, Frontend-Tests grün, Build fehlerfrei
- [ ] 6. Doku/OpenAPI aktualisieren (doc-agent) — separater Schritt, nicht Teil dieser Session
- [ ] 7. QA/E2E-Tests (qa-agent) — separater Schritt, nicht Teil dieser Session

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün
- [ ] Backend-API (C# 14) implementiert und getestet (TUnit)
- [ ] Angular-Komponenten implementiert und gestylt (DaisyUI 5), i18n DE/EN, ARIA
- [ ] Build (Backend + Frontend) fehlerfrei
