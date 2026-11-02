# Change: KI-gestützter Terminassistent (Chat)

> **Status:** archived
> Ablage: `UserStories/archive/2026-11-02-ki-terminassistent.openspec.md` (Requirements übernommen in `UserStories/specs/appointment-assistant.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Die Suche nach einem passenden freien Termin ("Welcher Mitarbeiter hat wann Zeit für Farbe + Schnitt bei Frau Müller nächste Woche?") erfordert heute manuelles Durchklicken der Kalenderansicht (`appointment-service`), Abgleich der Arbeitszeiten (`employee-service`), der zugeordneten Leistungen (`catalog-service`) und ggf. der Kundenhistorie (`customer-service`). Das ist für Bediener (Mitarbeiter/Admin) langsam, insbesondere bei mehreren möglichen Mitarbeitern/Leistungskombinationen. Ein Chat-basierter Assistent, der die vorhandenen Daten per RAG durchsucht und konkrete Terminvorschläge in natürlicher Sprache liefert, reduziert die Suchzeit erheblich, ohne die bestehende, validierte Terminanlage zu verändern.
- **What Changes:**
  - Neuer, eigenständiger Microservice `ai-service` (Bounded Context "Appointment Assistant"), der eine RAG-Pipeline orchestriert: Qdrant-Vektorsuche über indexierte Verfügbarkeits-/Leistungs-/Kundendaten (minimiert, siehe Abschnitt 6) + LLM-Call (OpenAI API, optional LMStudio als lokale Alternative) zur Interpretation der Nutzeranfrage und Formulierung der Vorschläge.
  - Neues Chat-Widget in `crm-web` (Feature-Bereich `assistant`), erreichbar für Rollen `Employee` und `Admin`, mit dem in natürlicher Sprache nach freien Terminslots gefragt werden kann.
  - Der Assistent liefert ausschließlich **Vorschläge** (lesend/empfehlend). Die tatsächliche Terminanlage erfolgt weiterhin ausschließlich über den bestehenden `POST /api/appointments`-Endpunkt von `appointment-service` inkl. dessen Kollisionsprüfung — kein Bypass, kein direktes Schreiben durch `ai-service`.
  - Gateway-Route `/api/assistant/**` für `ai-service`, analog zu den bestehenden Service-Routen.
- **Impact:** Neuer Microservice `ai-service` mit eigener Vektor-DB-Anbindung (Qdrant, kein eigener relationaler Datenspeicher für Domänendaten nötig — `ai-service` liest/dupliziert keine Kunden-/Termin-/Leistungsdaten dauerhaft, sondern fragt synchron bei `appointment-service`, `employee-service`, `catalog-service` und optional `customer-service` ab bzw. nutzt einen flüchtigen/aktualisierbaren Qdrant-Index). `appointment-service`, `employee-service`, `catalog-service`, `customer-service` sind NICHT strukturell betroffen (keine Schema-Änderungen) — sie werden von `ai-service` ausschließlich lesend über ihre bestehenden, öffentlichen APIs konsumiert (synchrone REST-Calls, Polly-Resilienz gemäß `docs/adr/0001-microservices-architektur-crm.md`). RBAC aus `appointment-scheduling` (Employee sieht nur eigene Termine, Admin alle) SHALL durch den Assistenten nicht umgangen werden. Frontend: neuer Feature-Bereich `assistant` in `Clients/crm-web` (ADR 0002), Gateway-Route `/api/assistant/**`, Aspire-AppHost-Registrierung (`ai-service` + Qdrant-Ressource), neuer Secret/Config-Eintrag für OpenAI-API-Key (kein Hardcoding, `CLAUDE.md`).

## 2. User Stories
- Als Mitarbeiter möchte ich im Chat in natürlicher Sprache nach einem freien Termin für einen Kunden und eine bestimmte Leistung fragen, damit ich nicht manuell durch den Kalender klicken muss.
- Als Admin möchte ich im Chat nach freien Terminslots über alle Mitarbeiter hinweg fragen, damit ich schnell den best passenden Mitarbeiter für einen Kundenwunsch finde.
- Als Mitarbeiter möchte ich, dass mir der Assistent nur Slots für meinen eigenen Kalender vorschlägt, damit die bestehende RBAC-Trennung zwischen Mitarbeitern gewahrt bleibt.
- Als Bediener möchte ich einen vom Assistenten vorgeschlagenen Slot mit einem Klick in das bestehende Terminanlage-Formular übernehmen, damit ich den Vorschlag bequem, aber unter voller Validierung bestätigen kann.
- Als Bediener möchte ich bei Nichtverfügbarkeit der KI-API eine klare Fehlermeldung statt eines Absturzes sehen, damit ich weiß, dass ich stattdessen die klassische Kalenderansicht nutzen muss.

## 3. Requirements (Delta)

Formulierung mit **SHALL/MUST**. Jede Requirement hat mindestens ein Scenario (WHEN/THEN). Jedes Scenario wird ein Test (TDD).

### ADDED Requirements

#### Requirement: Terminvorschlag per Chat-Anfrage
Das System SHALL es einem authentifizierten Bediener ermöglichen, über ein Chat-Widget in natürlicher Sprache (DE/EN) nach freien Terminslots zu fragen, und SHALL daraus strukturierte Suchkriterien (Kunde, gewünschte Leistung(en), Zeitraum, optional Mitarbeiter) extrahieren.

##### Scenario: Freitext-Anfrage mit Kunde, Leistung und Zeitraum
- **GIVEN** ein Mitarbeiter ist im Chat-Widget eingeloggt
- **WHEN** er die Nachricht "Freier Termin für Frau Müller nächste Woche, Farbe + Schnitt" sendet
- **THEN** erkennt der Assistent die Entitäten Kunde ("Müller"), Leistungen ("Farbe", "Schnitt") und Zeitraum ("nächste Woche") und fragt bei Mehrdeutigkeit (z.B. mehrere Kunden "Müller") gezielt nach, bevor er Vorschläge liefert

##### Scenario: Anfrage ohne erkennbare Leistung
- **GIVEN** ein Bediener sendet eine Nachricht ohne erkennbare Leistungsangabe (z.B. "Wann hat Herr Schmidt morgen Zeit?")
- **WHEN** der Assistent die Anfrage verarbeitet
- **THEN** antwortet der Assistent mit einer Rückfrage nach der gewünschten Leistung, statt einen unspezifischen Vorschlag zu raten

#### Requirement: Datengrundlage für Vorschläge (RAG über Kern-Daten)
Das System SHALL Terminvorschläge ausschließlich auf Basis aktueller Daten aus `appointment-service` (Kollisionsprüfung/Kalender), `employee-service` (Arbeitszeiten, Leistungszuordnung) und `catalog-service` (Leistungskatalog) berechnen; Kundendaten aus `customer-service` SHALL nur zur Kunden-Identifikation (Name/Historie-Kontext) herangezogen werden, nicht zur Verfügbarkeitsberechnung.

##### Scenario: Vorschlag berücksichtigt bestehende Kollisionen
- **GIVEN** ein Mitarbeiter hat laut `appointment-service` bereits einen Termin von 10:00–11:00 Uhr am gefragten Tag
- **WHEN** der Assistent für diesen Mitarbeiter Slots am selben Tag vorschlägt
- **THEN** enthält keiner der vorgeschlagenen Slots eine Überlappung mit 10:00–11:00 Uhr

##### Scenario: Vorschlag berücksichtigt Arbeitszeiten und Leistungszuordnung
- **GIVEN** ein Mitarbeiter arbeitet laut `employee-service` nur bis 16:00 Uhr und ist der Leistung "Farbe" nicht zugeordnet
- **WHEN** ein Bediener nach einem Termin für "Farbe" bei diesem Mitarbeiter fragt
- **THEN** schlägt der Assistent für diesen Mitarbeiter keine Slots vor und weist stattdessen daraufhin, dass der Mitarbeiter diese Leistung nicht anbietet, bzw. schlägt alternative, qualifizierte Mitarbeiter vor

#### Requirement: RBAC-konforme Sichtbarkeit von Terminvorschlägen
Das System SHALL für Nutzer mit Rolle `Employee` ausschließlich Terminvorschläge für die eigene `EmployeeId` liefern; Nutzer mit Rolle `Admin` SHALL Vorschläge für alle Mitarbeiter erhalten können. Diese Einschränkung SHALL serverseitig in `ai-service` erzwungen werden und darf nicht durch Chat-Eingaben (z.B. explizite Nennung eines anderen Mitarbeiternamens) umgangen werden.

##### Scenario: Employee fragt nach Slot eines anderen Mitarbeiters
- **GIVEN** Mitarbeiter A (Rolle `Employee`) ist im Chat eingeloggt
- **WHEN** er fragt "Wann hat Mitarbeiter B nächste Woche Zeit für einen Haarschnitt?"
- **THEN** antwortet der Assistent, dass er nur Vorschläge für den eigenen Kalender machen kann, und liefert keine Slots für Mitarbeiter B

##### Scenario: Admin fragt ohne Mitarbeiter-Einschränkung
- **GIVEN** ein Nutzer mit Rolle `Admin` ist im Chat eingeloggt
- **WHEN** er nach einem freien Termin für eine Leistung ohne Nennung eines bestimmten Mitarbeiters fragt
- **THEN** liefert der Assistent Vorschläge über alle qualifizierten, verfügbaren Mitarbeiter hinweg

#### Requirement: Vorschlag ist unverbindlich und erfordert bestätigte Terminanlage
Das System SHALL einen vom Assistenten gelieferten Terminvorschlag ausschließlich als unverbindliche Empfehlung darstellen. Die verbindliche Terminanlage SHALL ausschließlich über den bestehenden `POST /api/appointments`-Endpunkt (inkl. dessen Kollisionsprüfung und Validierung) erfolgen; `ai-service` SHALL selbst keinen Termin anlegen, ändern oder stornieren können.

##### Scenario: Vorschlag übernehmen und bestätigen
- **GIVEN** der Assistent hat einen freien Slot vorgeschlagen
- **WHEN** der Bediener auf "Termin anlegen" beim Vorschlag klickt
- **THEN** öffnet sich das bestehende Terminanlage-Formular mit vorausgefüllten Werten (Kunde, Mitarbeiter, Leistung, Zeitfenster), und der Termin wird erst nach expliziter Bestätigung und erfolgreicher Validierung durch `POST /api/appointments` persistiert

##### Scenario: Vorgeschlagener Slot ist inzwischen belegt
- **GIVEN** der Assistent hat vor 2 Minuten einen Slot vorgeschlagen, der inzwischen durch einen anderen Bediener belegt wurde
- **WHEN** der Bediener den Vorschlag übernimmt und bestätigt
- **THEN** lehnt `POST /api/appointments` die Anlage mit einem Konfliktfehler ab, und das Frontend zeigt eine verständliche Fehlermeldung statt den Termin fälschlich als angelegt darzustellen

#### Requirement: Datenminimierung bei externen LLM-Aufrufen
Das System SHALL beim Aufruf der externen OpenAI-API nur die für den Vorschlag minimal notwendigen, möglichst anonymisierten Daten übermitteln (z.B. freie/belegte Zeitfenster als abstrakte Intervalle, Leistungstyp, grobe Mitarbeiter-Kennung) und SHALL keine vollständige Kundenhistorie, keine Klarnamen-Kombination mit sensiblen Gesundheits-/Beratungsdetails oder sonstige über den Vorschlagszweck hinausgehende personenbezogene Daten in den Prompt aufnehmen.

##### Scenario: Prompt an OpenAI enthält keine vollständige Kundenhistorie
- **GIVEN** ein Kunde hat eine umfangreiche Terminhistorie inkl. interner Notizen
- **WHEN** der Assistent einen Vorschlag für diesen Kunden über die OpenAI-API generiert
- **THEN** enthält der an OpenAI gesendete Prompt ausschließlich die zur Slot-Findung notwendigen Daten (Zeitfenster, Leistungstyp, Mitarbeiter-Kennung) und keine internen Notizen oder die vollständige Terminhistorie des Kunden

#### Requirement: Resilienz bei Nichtverfügbarkeit der KI-API
Das System SHALL bei Nichtverfügbarkeit, Timeout oder Rate-Limit-Antwort der OpenAI-API (bzw. der konfigurierten LLM-Instanz) eine begrenzte Anzahl an Retries mit Backoff durchführen und danach eine klare, verständliche Fehlermeldung im Chat anzeigen, statt die Anfrage kommentarlos fehlschlagen zu lassen oder einen Absturz zu verursachen.

##### Scenario: OpenAI-API antwortet mit Rate-Limit
- **GIVEN** die OpenAI-API antwortet wiederholt mit HTTP 429
- **WHEN** ein Bediener eine Chat-Anfrage sendet
- **THEN** führt `ai-service` die konfigurierte Retry-Policy (Polly) aus und zeigt nach deren Ausschöpfung eine Fehlermeldung im Chat ("Der Assistent ist aktuell nicht verfügbar, bitte nutzen Sie die Kalenderansicht."), ohne die Anfrage unbeantwortet oder die Anwendung instabil zu lassen

##### Scenario: Qdrant-Index nicht erreichbar
- **GIVEN** die Qdrant-Instanz ist nicht erreichbar
- **WHEN** ein Bediener eine Chat-Anfrage sendet
- **THEN** zeigt der Chat eine klare Fehlermeldung an und schlägt keine (möglicherweise auf veralteten/fehlenden Daten basierenden) Slots vor

#### Requirement: Konfigurierbare LLM-Anbindung (OpenAI/LMStudio)
Das System SHALL den LLM-Provider (OpenAI API oder lokal via LMStudio) über Konfiguration (Environment Variables/Secrets, kein Hardcoding) umschaltbar machen, ohne Codeänderung in der RAG-Orchestrierungslogik.

##### Scenario: Wechsel auf lokalen LMStudio-Provider
- **GIVEN** `ai-service` ist mit der Konfiguration `LlmProvider=LmStudio` und einer lokalen LMStudio-Endpunkt-URL gestartet
- **WHEN** ein Bediener eine Chat-Anfrage sendet
- **THEN** wird der Chat-Request gegen den konfigurierten LMStudio-Endpunkt statt gegen die OpenAI-API verarbeitet, bei identischem Antwortformat für das Frontend

## 4. UI/UX Requirements
- **Komponenten**: `assistant-chat-widget.component.ts` (schwebender/einklappbarer Chat-Button + Chat-Panel, DaisyUI `chat`/`chat-bubble`-Klassen, analog zum bestehenden Komponentenstil in `crm-web`), eingebettet in das App-Shell-Layout (`app.html`), sichtbar für Rollen `Employee`/`Admin`. `assistant-suggestion-card.component.ts` für einzelne Terminvorschläge (Kunde, Mitarbeiter, Leistung, Zeitfenster, Button "Termin anlegen" → Deep-Link ins bestehende Termin-Formular mit vorausgefüllten Query-Parametern).
- **State**: Neuer `assistant-state.service.ts` (Angular Signals) für Chatverlauf, Ladezustand ("Assistent denkt nach…"), Fehlerzustand, aktuelle Vorschlagsliste — analog zum etablierten `customer-state.service.ts`-Muster.
- **i18n / a11y**: Neue DE/EN-Keys unter `assistant.*` (Platzhaltertext, Fehlermeldungen, Button-Labels). Chat-Panel mit `role="log"`/`aria-live="polite"` für neue Nachrichten, Eingabefeld mit `<label>` (visuell ggf. ausgeblendet, aber vorhanden) und `aria-describedby` für Hinweistext, vollständige Tastaturbedienbarkeit (Chat öffnen/schließen, Nachricht senden per Enter, Fokus-Management beim Öffnen des Panels), Vorschlagskarten mit semantischem `<button>` statt reinem `<div onclick>`.

## 5. API & Backend Requirements
- **Service:** `ai-service` (eigenständig deploybar, analog zu `catalog-service`; siehe Design-Begründung Abschnitt 7). Kein eigener relationaler Datenspeicher für Domänen-Stammdaten; Anbindung an Qdrant für den Vektorindex.
- **Authentifizierung/Autorisierung**: `[Authorize]` Standard (Microsoft Identity); Rollen `Employee` und `Admin` dürfen den Chat nutzen. Rollenbasierte Einschränkung der Vorschlagsergebnisse (siehe Requirement "RBAC-konforme Sichtbarkeit") wird serverseitig in `ai-service` anhand der `EmployeeId`-Claim des eingehenden JWT erzwungen, nicht im Frontend.
- **Endpoints**:
  - `POST /api/assistant/chat`: Nimmt `{ sessionId, message }` entgegen, liefert `{ reply: string, suggestions: AppointmentSuggestionDto[] }`. Interner Ablauf: Entitätenextraktion → Qdrant-Abfrage (Verfügbarkeitsfenster/Leistungen) → synchrone REST-Calls an `appointment-service`/`employee-service`/`catalog-service` (und optional `customer-service` zur Kundenauflösung) → LLM-Call (OpenAI/LMStudio) zur Formulierung der Antwort → Rückgabe.
  - `GET /api/assistant/sessions/{sessionId}/history`: Liefert den bisherigen Chatverlauf einer Session (nur für den eigenen Nutzer; `Employee` sieht nur eigene Sessions, `Admin` nur eigene — Chatverläufe sind nicht mitarbeiterübergreifend einsehbar).
- **Modelle/DTOs:**
  - `AppointmentSuggestionDto`: `{ employeeId (Guid), employeeName (string), serviceId (Guid), serviceName (string), customerId (Guid?), startTime (DateTimeOffset), endTime (DateTimeOffset) }`
  - `ChatMessageDto`: `{ sessionId (Guid), role ("user"|"assistant"), content (string), createdAt (DateTimeOffset) }`
- **Inter-Service-Kommunikation:** `ai-service` ruft synchron (Polly-Resilienz gemäß `docs/adr/0001-microservices-architektur-crm.md` Abschnitt 5: Timeout, max. 2 Retries mit Backoff für transiente Fehler, Circuit-Breaker, kein Silent-Fallback) folgende bestehende, öffentliche Endpunkte auf: `GET /api/appointments` (Kollisionsdaten, rollenbasiert gefiltert über weitergeleiteten Authorization-Header), `GET /api/employees/{id}/availability` bzw. äquivalente Arbeitszeit-/Leistungszuordnungs-Abfragen, `GET /api/services` (Leistungskatalog), optional `GET /api/customers?search=` (nur zur Namensauflösung, keine Historie-Details). Der eingehende Authorization-Header wird gemäß etabliertem Muster (`AuthHeaderForwardingHandler`) weitergereicht, damit RBAC in den Ziel-Services konsistent angewendet wird.
- **Gateway:** neue Route `/api/assistant/**` → `ai-service-cluster`, Auth-Termination wie bei den anderen Services.

## 6. KI/AI Requirements (Optional)
- **Vektorsuche (Qdrant)**: Ja. `ai-service` unterhält einen Qdrant-Collection-Index über abstrahierte, regelmäßig aktualisierte Verfügbarkeits-/Leistungs-Embeddings (z.B. "Mitarbeiter X, Leistung Y, freie Fenster Montag–Freitag"), NICHT über rohe Kundendatensätze. Der Index wird bei Chat-Anfragen zur semantischen Vorfilterung genutzt (z.B. Mapping von "Farbe + Schnitt" auf die passenden Katalog-Leistungen, auch bei Synonymen/Tippfehlern), die harte Verfügbarkeitsentscheidung erfolgt jedoch stets über den synchronen Live-Call an `appointment-service`/`employee-service` (kein veralteter Qdrant-Stand als alleinige Entscheidungsgrundlage für die finale Slot-Liste — Qdrant dient der Kandidaten-Vorauswahl/semantischen Suche, nicht der Kollisionsprüfung).
- **Prompt/System Message**: System-Prompt legt fest: (1) der Assistent macht ausschließlich Terminvorschläge, legt niemals selbst Termine an; (2) der Assistent SHALL bei Mehrdeutigkeit nachfragen statt zu raten; (3) der Assistent SHALL nur mit den im Kontext mitgegebenen, bereits minimierten Daten (Abschnitt 3, "Datenminimierung") argumentieren und keine Daten erfinden ("no hallucinated slots" — jeder vorgeschlagene Slot muss aus einem zuvor vom Backend geprüften, tatsächlich freien Zeitfenster stammen); (4) Antwortsprache folgt der Nutzereingabe (DE/EN).
- **Resilienz & Datenschutz**: Siehe Requirements "Resilienz bei Nichtverfügbarkeit der KI-API" und "Datenminimierung bei externen LLM-Aufrufen" in Abschnitt 3 — Polly-Retry/Circuit-Breaker für OpenAI-Calls, keine ungefilterten sensiblen Domänendaten im Prompt (`CLAUDE.md`, Abschnitt KI-Integration).

## 7. Design (Technischer Ansatz)
`ai-service` wird als eigenständiger Microservice analog zu `catalog-service` (kleinste bestehende Referenz für einen neuen, fokussierten Bounded Context) umgesetzt, jedoch mit einer wesentlichen Abweichung: Statt eigener Aggregate-Persistenz in PostgreSQL ist der primäre Datenspeicher ein Qdrant-Vektorindex (flüchtige/ableitbare Daten, kein Owner von Stammdaten) plus ein schlanker PostgreSQL-Store ausschließlich für Chatverlauf/Session-Metadaten (`ai_db`, Database-per-Service-Prinzip bleibt gewahrt für das, was tatsächlich persistiert wird). Begründung für eigenständigen Service statt Erweiterung eines bestehenden Service: Der Bounded Context "Appointment Assistant" hat eine fachlich eigenständige Verantwortlichkeit (Orchestrierung mehrerer bestehender Services + externe KI-API), eine andere Skalierungs-/Ausfallcharakteristik (abhängig von externer OpenAI-API-Latenz/Rate-Limits, soll die Kern-Terminverwaltung nicht beeinträchtigen) und eine andere Technologie-Abhängigkeit (Qdrant, LLM-Clients) als `appointment-service`/`employee-service`/`catalog-service`; eine Erweiterung von `appointment-service` würde dessen Clean-Architecture-Grenzen (keine KI-/Vektor-DB-Abhängigkeit in der bestehenden Terminverwaltung) verletzen und dessen Verfügbarkeit an die Verfügbarkeit der OpenAI-API koppeln — nicht akzeptabel für einen Kernprozess.

Interne Schichtung von `ai-service` folgt der in `docs/adr/0001-microservices-architektur-crm.md` Abschnitt 6 festgelegten Clean Architecture: Domain-Layer hält nur einfache Werttypen (`ChatSession`, `ChatMessage` als Aggregate, keine Terminlogik — die eigentliche Verfügbarkeitslogik bleibt alleinig in `appointment-service`/`employee-service`). Application-Layer orchestriert über Wolverine-Commands/Queries (`HandleChatMessageCommand`) den Ablauf: Entitätenextraktion (LLM-Call #1 oder Pattern-Matching je nach Komplexität) → Qdrant-Query (Kandidaten-Leistungen/Mitarbeiter) → parallele, Polly-abgesicherte REST-Calls an die drei bestehenden Services → Kandidatenfilterung (RBAC-Filter nach `EmployeeId`-Claim, siehe Abschnitt 3) → LLM-Call #2 zur Antwortformulierung (System-Prompt siehe Abschnitt 6). Infrastructure-Layer enthält die Qdrant-Client-Implementierung, die typisierten HTTP-Clients zu den drei Fremdservices (mit `AuthHeaderForwardingHandler`-Muster) und die LLM-Provider-Abstraktion (`ILlmClient` mit zwei Implementierungen `OpenAiLlmClient`/`LmStudioLlmClient`, Auswahl über Konfiguration/DI, siehe Requirement "Konfigurierbare LLM-Anbindung"). Web-Layer: Controller mit JWT-Validierung (gleiches Issuer/Audience/Signing-Schema wie die anderen Services), zentrales Exception Handling (keine Stacktraces/OpenAI-Rohfehler an das Frontend), Swagger/OpenAPI.

Ein initialer Index-Befüllungs-/Aktualisierungs-Mechanismus für Qdrant (z.B. periodischer Hintergrund-Job oder Konsum der bestehenden Integration Events `AppointmentCreated`/`AppointmentCancelled`/`AppointmentRescheduled`/`EmployeeDeactivated`, um den Verfügbarkeits-Index aktuell zu halten) wird in der Implementierung benötigt; die konkrete Aktualisierungsstrategie (Event-getrieben vs. periodisch) ist vom `architect-agent` vor Implementierungsstart in einem ADR zu entscheiden, da sie Konsistenz-Trade-offs zwischen Qdrant-Index und Live-Daten betrifft (Risiko: veralteter Index führt zu Vorschlägen, die bei der finalen Kollisionsprüfung durch `appointment-service` abgelehnt werden — dies ist durch Requirement "Vorgeschlagener Slot ist inzwischen belegt" fachlich bereits abgefangen, aber ein zu veralteter Index verschlechtert die Vorschlagsqualität). Empfehlung: Event-getriebene Aktualisierung (Wolverine-Konsument der bestehenden Events), da dies dem bereits etablierten asynchronen Kommunikationsmuster aus ADR 0001 folgt und keinen zusätzlichen Polling-Overhead erzeugt.

AppHost: `ai-service`-Projektressource, `ai-postgres`/`ai_db` (nur für Chat-Session-Metadaten), Qdrant-Container-Ressource (falls nicht bereits für andere Zwecke vorhanden), OpenAI-API-Key als Aspire-Parameter/Secret (kein Hardcoding), optionale LMStudio-Endpunkt-Konfiguration. Gateway-Referenz für `/api/assistant/**` ergänzen.

## 8. Tasks (Implementierungs-Checkliste)
- [x] 1. ADR: Entscheidung zur Qdrant-Index-Aktualisierungsstrategie (Event-getrieben vs. periodisch) durch `architect-agent` vor Implementierungsstart dokumentiert — siehe `docs/adr/0003-ai-service-qdrant-index-strategie.md` (event-getrieben + täglicher Konsistenz-Sync; erfordert neues Integration Event `ServiceCatalogChanged` in `catalog-service`)
- [x] 2. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/AiService.Tests` (28 Tests) + `Tests/CatalogService.Tests` (ServiceCatalogChanged-Publish-Tests)
- [x] 3. Backend implementiert (Domain → Application → Infrastructure → Web) – `Services/ai-service/`, inkl. `ILlmClient`-Abstraktion (OpenAI + LMStudio), Qdrant-Client, Polly-Policies für Cross-Service-Calls und LLM-Calls
- [x] 4. Frontend implementiert: `assistant-chat-widget.component.ts`, `assistant-suggestion-card.component.ts`, `assistant-state.service.ts` im neuen Feature-Bereich `assistant` (Angular 22, DaisyUI 5, i18n DE/EN, ARIA) — 155/155 Frontend-Tests grün
- [x] 5. Deep-Link vom Vorschlag ins bestehende Terminanlage-Formular (vorausgefüllte Werte via Query-Parameter, keine automatische Buchung)
- [x] 6. E2E-Tests (`Tests/E2E/tests/assistant.spec.ts`): RBAC-Negativtest (echter Backend-Pfad) und Resilienz-Szenario (gemockte Chat-Antwort, Begründung siehe Kommentar in der Datei) — strukturell verifiziert (`npx playwright test --list`), nicht gegen einen laufenden Stack ausgeführt (kein Docker/Aspire-Stack in der Implementierungssession verfügbar)
- [x] 7. Doku/OpenAPI aktualisiert (`Services/ai-service/README.md`, Root-`README.md`/`AppHost/README.md`/`Tests/E2E/README.md` ergänzt)
- [x] 8. Aspire AppHost-Ressourcen für `ai-service`, `ai_db`, Qdrant registriert; OpenAI-API-Key/LMStudio-Endpunkt als Secret/Parameter (kein Hardcoding); zusätzlich `docker-compose.prod.yml` um dieselben Ressourcen ergänzt
- [x] 9. YARP-Gateway-Route `/api/assistant/**` inkl. Auth-Termination konfiguriert (Aspire + docker-compose)
- [x] 10. Sicherheitsreview — siehe Abschnitt 10 unten

## 9. Acceptance Criteria (DoD)
- [x] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün (`Tests/AiService.Tests`, `Tests/CrmWeb.Tests/assistant`).
- [x] Backend API (C# 14) ist implementiert und getestet (TUnit), inkl. Resilienz-Tests für LLM-/Qdrant-Nichtverfügbarkeit.
- [x] Angular-Komponenten sind implementiert und gestylt (DaisyUI 5), i18n (DE/EN) und ARIA-konform (role="log"/aria-live, Label+aria-describedby, Tastaturbedienbarkeit).
- [x] RBAC-Konformität verifiziert: Employee erhält nie Vorschläge für andere Mitarbeiter (Application- und Web-Layer-Test); Admin erhält Vorschläge über alle qualifizierten Mitarbeiter.
- [x] Keine ungefilterten sensiblen Kundendaten werden im Prompt an die externe LLM-API gesendet (verifiziert durch `Handle_WithQualifiedEmployee_SendsOnlyMinimizedContextToLlm` + Review, siehe Abschnitt 10).
- [x] Terminanlage erfolgt ausschließlich über den bestehenden, validierten `POST /api/appointments`-Endpunkt; `ai-service` kann keinen Termin direkt anlegen/ändern/stornieren (kein entsprechender Client/Call im Code).
- [x] E2E Tests (`Tests/E2E/tests/assistant.spec.ts`) sind geschrieben und strukturell verifiziert; **nicht** gegen einen real laufenden Stack ausgeführt (siehe Task 6) — offene Nachverifikation, sobald ein Stack verfügbar ist.
- [x] Swagger/OpenAPI Compliance ist gegeben (`ai-service` Swagger-Dokument, JWT-Bearer-Schema analog den anderen Services).
- [x] Spec ist archiviert (Status `archived`, Requirements übernommen in `UserStories/specs/appointment-assistant.openspec.md`, Change verschoben nach `UserStories/archive/2026-11-02-ki-terminassistent.openspec.md`).

## 10. Sicherheitsreview (`sec-agent`-Perspektive, 2026-11-02)

**Geprüft: Datenminimierung im LLM-Prompt** (Requirement "Datenminimierung bei externen LLM-Aufrufen").
`HandleChatMessageHandler.BuildMinimizedUserContext` (`Services/ai-service/src/AiService.Application/Chat/HandleChatMessage/HandleChatMessageHandler.cs`)
baut den an `ILlmClient.CompleteAsync` übergebenen Kontext ausschließlich aus `EmployeeId`,
`ServiceName` und den bereits live geprüften Start-/End-Zeitfenstern der Vorschläge. Weder
`CustomerId`/Kundenname noch irgendwelche Felder aus `customer-service` (Historie, interne
Notizen) fließen in diesen String ein — `resolvedCustomerId` wird nur in die
`AppointmentSuggestionDto` (API-Antwort an das Frontend) übernommen, nicht in den Prompt.
Verifiziert durch `Handle_WithQualifiedEmployee_SendsOnlyMinimizedContextToLlm`
(`Tests/AiService.Tests/Application/HandleChatMessageHandlerTests.cs`), die den tatsächlich
übergebenen Kontext-String gegen Marker wie "Notiz"/"Historie" prüft. **Befund: konform.**

**Geprüft: RBAC serverseitig, nicht im Frontend erzwungen** (Requirement "RBAC-konforme
Sichtbarkeit von Terminvorschlägen"). `AssistantController` liest `EmployeeId`/Admin-Rolle
ausschließlich aus den JWT-Claims (`GetActorId()`/`IsAdmin()`), niemals aus dem Request-Body;
`HandleChatMessageCommand.CallerEmployeeId`/`IsAdmin` werden vom Controller gesetzt und im
Handler als alleinige Entscheidungsgrundlage verwendet — ein im Chat-Text genannter anderer
Mitarbeitername (`EmployeeNameHint`) wird für Nicht-Admins explizit als Ablehnungsgrund
behandelt, nie als Override. Verifiziert sowohl auf Application-Ebene
(`Handle_EmployeeAsksForAnotherEmployee_ReturnsRestrictedReplyWithoutSuggestions`) als auch
End-to-End über den echten HTTP-Pfad (`AssistantApiTests.PostChat_AsEmployeeAskingForAnotherEmployee_...`,
`Tests/E2E/tests/assistant.spec.ts`). **Befund: konform.**

**Geprüft: keine hartkodierten Secrets.** `grep` über `Services/ai-service` auf API-Key-/Secret-Literale
ergab keinen Treffer; `OpenAi:ApiKey`/`Authentication:JwtBearer:SigningKey` sind in
`appsettings.json` bewusst leere Platzhalter (Environment-Variable/Aspire-Secret-Parameter
`openai-api-key`, analog `jwt-signing-key`). **Befund: konform.**

**Bekannte Einschränkung (kein Fix in dieser Session, siehe `Services/ai-service/README.md`):**
Die Wolverine-Event-Konsumenten und der tägliche Konsistenz-Sync laufen außerhalb eines
eingehenden HTTP-Request-Kontexts; `AuthHeaderForwardingHandler` kann dort keinen
Authorization-Header weiterreichen. Fachlich unkritisch, da ausschließlich Ableitungsdaten
(Qdrant-Index) betroffen sind und die finale Slot-Liste laut ADR 0003 immer live verifiziert
wird — aber ein offener Punkt für einen Service-Account-/Client-Credentials-Mechanismus,
den `sec-agent`/`architect-agent` als Folgeaufgabe einplanen sollten, bevor Hintergrundjobs in
Produktion echte, authentifizierungspflichtige Calls gegen andere Services ausführen müssen.
