# Capability: Appointment Assistant (KI-Terminassistent)

> **Status:** active (Backend/Frontend implementiert; E2E-Tests geschrieben, aber noch nicht gegen
> einen real laufenden Stack ausgeführt — siehe Abschnitt "Tests")
> Implementiert durch `ai-service` (Bounded Context "Appointment Assistant") + Feature-Bereich
> `assistant` in `Clients/crm-web`. Archivierte Change-Historie:
> `UserStories/archive/2026-11-02-ki-terminassistent.openspec.md`.

## Zweck
Chat-basierter Assistent, der per RAG (Qdrant + OpenAI/LMStudio) aus Freitext-Anfragen konkrete,
aber unverbindliche Terminvorschläge ableitet, um das manuelle Durchklicken von Kalender,
Arbeitszeiten und Leistungskatalog zu ersetzen — ohne die bestehende, validierte Terminanlage zu
verändern.

## Requirements

#### Requirement: Terminvorschlag per Chat-Anfrage
Das System SHALL es einem authentifizierten Bediener ermöglichen, über ein Chat-Widget in
natürlicher Sprache (DE/EN) nach freien Terminslots zu fragen, und SHALL daraus strukturierte
Suchkriterien (Kunde, gewünschte Leistung(en), Zeitraum, optional Mitarbeiter) extrahieren.

##### Scenario: Freitext-Anfrage mit Kunde, Leistung und Zeitraum
- **GIVEN** ein Mitarbeiter ist im Chat-Widget eingeloggt
- **WHEN** er die Nachricht "Freier Termin für Frau Müller nächste Woche, Farbe + Schnitt" sendet
- **THEN** erkennt der Assistent die Entitäten Kunde ("Müller"), Leistungen ("Farbe", "Schnitt")
  und Zeitraum ("nächste Woche") und fragt bei Mehrdeutigkeit (z.B. mehrere Kunden "Müller")
  gezielt nach, bevor er Vorschläge liefert

##### Scenario: Anfrage ohne erkennbare Leistung
- **GIVEN** ein Bediener sendet eine Nachricht ohne erkennbare Leistungsangabe
- **WHEN** der Assistent die Anfrage verarbeitet
- **THEN** antwortet der Assistent mit einer Rückfrage nach der gewünschten Leistung, statt einen
  unspezifischen Vorschlag zu raten

#### Requirement: Datengrundlage für Vorschläge (RAG über Kern-Daten)
Das System SHALL Terminvorschläge ausschließlich auf Basis aktueller Daten aus
`appointment-service` (Kollisionsprüfung/Kalender), `employee-service` (Arbeitszeiten,
Leistungszuordnung) und `catalog-service` (Leistungskatalog) berechnen; Kundendaten aus
`customer-service` SHALL nur zur Kunden-Identifikation herangezogen werden, nicht zur
Verfügbarkeitsberechnung. Der Qdrant-Index dient ausschließlich der semantischen
Kandidaten-Vorauswahl, niemals der harten Verfügbarkeitsentscheidung
(siehe `docs/adr/0003-ai-service-qdrant-index-strategie.md`, Abschnitt 4).

##### Scenario: Vorschlag berücksichtigt bestehende Kollisionen
- **GIVEN** ein Mitarbeiter hat laut `appointment-service` bereits einen Termin von 10:00–11:00
  Uhr am gefragten Tag
- **WHEN** der Assistent für diesen Mitarbeiter Slots am selben Tag vorschlägt
- **THEN** enthält keiner der vorgeschlagenen Slots eine Überlappung mit 10:00–11:00 Uhr

##### Scenario: Vorschlag berücksichtigt Arbeitszeiten und Leistungszuordnung
- **GIVEN** ein Mitarbeiter arbeitet laut `employee-service` nur bis 16:00 Uhr und ist der
  Leistung "Farbe" nicht zugeordnet
- **WHEN** ein Bediener nach einem Termin für "Farbe" bei diesem Mitarbeiter fragt
- **THEN** schlägt der Assistent für diesen Mitarbeiter keine Slots vor

#### Requirement: RBAC-konforme Sichtbarkeit von Terminvorschlägen
Das System SHALL für Nutzer mit Rolle `Employee` ausschließlich Terminvorschläge für die eigene
`EmployeeId` liefern; Nutzer mit Rolle `Admin` SHALL Vorschläge für alle qualifizierten Mitarbeiter
erhalten können. Diese Einschränkung SHALL serverseitig in `ai-service` (anhand der JWT-Claims)
erzwungen werden und darf nicht durch Chat-Eingaben umgangen werden.

##### Scenario: Employee fragt nach Slot eines anderen Mitarbeiters
- **GIVEN** Mitarbeiter A (Rolle `Employee`) ist im Chat eingeloggt
- **WHEN** er fragt "Wann hat Mitarbeiter B nächste Woche Zeit für einen Haarschnitt?"
- **THEN** antwortet der Assistent, dass er nur Vorschläge für den eigenen Kalender machen kann,
  und liefert keine Slots für Mitarbeiter B

##### Scenario: Admin fragt ohne Mitarbeiter-Einschränkung
- **GIVEN** ein Nutzer mit Rolle `Admin` ist im Chat eingeloggt
- **WHEN** er nach einem freien Termin für eine Leistung ohne Nennung eines bestimmten
  Mitarbeiters fragt
- **THEN** liefert der Assistent Vorschläge über alle qualifizierten, verfügbaren Mitarbeiter hinweg

#### Requirement: Vorschlag ist unverbindlich und erfordert bestätigte Terminanlage
Das System SHALL einen vom Assistenten gelieferten Terminvorschlag ausschließlich als
unverbindliche Empfehlung darstellen. Die verbindliche Terminanlage SHALL ausschließlich über den
bestehenden `POST /api/appointments`-Endpunkt erfolgen; `ai-service` SHALL selbst keinen Termin
anlegen, ändern oder stornieren können.

##### Scenario: Vorschlag übernehmen und bestätigen
- **GIVEN** der Assistent hat einen freien Slot vorgeschlagen
- **WHEN** der Bediener auf "Termin anlegen" beim Vorschlag klickt
- **THEN** öffnet sich das bestehende Terminanlage-Formular mit vorausgefüllten Werten (Kunde,
  Mitarbeiter, Leistung, Zeitfenster), und der Termin wird erst nach expliziter Bestätigung und
  erfolgreicher Validierung durch `POST /api/appointments` persistiert

##### Scenario: Vorgeschlagener Slot ist inzwischen belegt
- **GIVEN** der Assistent hat vor 2 Minuten einen Slot vorgeschlagen, der inzwischen belegt wurde
- **WHEN** der Bediener den Vorschlag übernimmt und bestätigt
- **THEN** lehnt `POST /api/appointments` die Anlage mit einem Konfliktfehler ab, und das
  Frontend zeigt eine verständliche Fehlermeldung statt den Termin fälschlich als angelegt
  darzustellen

#### Requirement: Datenminimierung bei externen LLM-Aufrufen
Das System SHALL beim Aufruf der externen LLM-API (OpenAI/LMStudio) nur die für den Vorschlag
minimal notwendigen, abstrahierten Daten übermitteln (abstrakte Zeitfenster, Leistungstyp,
Mitarbeiter-Kennung) und SHALL keine vollständige Kundenhistorie, keine Klarnamen-Kombination mit
sensiblen Details oder sonstige über den Vorschlagszweck hinausgehende personenbezogene Daten in
den Prompt aufnehmen.

##### Scenario: Prompt enthält keine vollständige Kundenhistorie
- **GIVEN** ein Kunde hat eine umfangreiche Terminhistorie inkl. interner Notizen
- **WHEN** der Assistent einen Vorschlag für diesen Kunden generiert
- **THEN** enthält der an die LLM-API gesendete Prompt ausschließlich Zeitfenster/Leistungstyp/
  Mitarbeiter-Kennung und keine internen Notizen oder die vollständige Terminhistorie

#### Requirement: Resilienz bei Nichtverfügbarkeit der KI-API
Das System SHALL bei Nichtverfügbarkeit, Timeout oder Rate-Limit-Antwort der LLM-API bzw. des
Qdrant-Index eine begrenzte Anzahl an Retries mit Backoff durchführen und danach eine klare,
verständliche Fehlermeldung im Chat anzeigen, statt die Anfrage kommentarlos fehlschlagen zu
lassen oder einen Absturz zu verursachen.

##### Scenario: LLM-API antwortet mit Rate-Limit
- **GIVEN** die LLM-API antwortet wiederholt mit HTTP 429
- **WHEN** ein Bediener eine Chat-Anfrage sendet
- **THEN** führt `ai-service` die konfigurierte Retry-Policy aus und zeigt nach deren
  Ausschöpfung die Fehlermeldung "Der Assistent ist aktuell nicht verfügbar, bitte nutzen Sie die
  Kalenderansicht." im Chat

##### Scenario: Qdrant-Index nicht erreichbar
- **GIVEN** die Qdrant-Instanz ist nicht erreichbar
- **WHEN** ein Bediener eine Chat-Anfrage sendet
- **THEN** zeigt der Chat eine klare Fehlermeldung an und schlägt keine Slots vor

#### Requirement: Konfigurierbare LLM-Anbindung (OpenAI/LMStudio)
Das System SHALL den LLM-Provider über Konfiguration (Environment Variables/Secrets, kein
Hardcoding) umschaltbar machen, ohne Codeänderung in der RAG-Orchestrierungslogik.

##### Scenario: Wechsel auf lokalen LMStudio-Provider
- **GIVEN** `ai-service` ist mit `LlmProvider=LmStudio` und einer lokalen LMStudio-Endpunkt-URL
  gestartet
- **WHEN** ein Bediener eine Chat-Anfrage sendet
- **THEN** wird der Chat-Request gegen den konfigurierten LMStudio-Endpunkt statt gegen die
  OpenAI-API verarbeitet, bei identischem Antwortformat für das Frontend

## API
- `POST /api/assistant/chat`: `{ sessionId?, message }` → `{ reply, suggestions: AppointmentSuggestionDto[] }`.
- `GET /api/assistant/sessions/{sessionId}/history`: Chatverlauf, nur für den eigenen Nutzer.
- `AppointmentSuggestionDto`: `{ employeeId, employeeName, serviceId, serviceName, customerId?, startTime, endTime }`.
- Rollen: `Employee`/`Admin` für beide Endpoints; RBAC-Filterung serverseitig anhand der
  `EmployeeId`-Claim.
- Siehe `Services/ai-service/README.md` und Swagger (`/swagger`, Development).

## Architektur-Verweise
- `docs/adr/0001-microservices-architektur-crm.md` (Servicegrenzen, Resilienz-Muster, Clean Architecture)
- `docs/adr/0003-ai-service-qdrant-index-strategie.md` (Qdrant-Index-Aktualisierungsstrategie,
  Konsistenzmodell, Folgeaufgabe `ServiceCatalogChangedIntegrationEvent` in `catalog-service`)
- `docs/adr/0002-frontend-konsolidierung.md` (Feature-Bereich `assistant` in `Clients/crm-web`)
- `ai-service` legt/ändert/storniert nie selbst einen Termin — ausschließlich
  `POST /api/appointments` in `appointment-service`.
- KNOWN LIMITATION: Wolverine-Event-Konsumenten/täglicher Konsistenz-Sync laufen ohne
  HTTP-Request-Kontext und können daher keinen Authorization-Header weiterreichen — fachlich
  unkritisch für reine Ableitungsdaten (siehe `Services/ai-service/README.md`), aber offener Punkt
  für einen künftigen Service-Account-Mechanismus.

## Tests
- `Tests/AiService.Tests` (Backend, 28 Tests: Domain, Application inkl. RBAC-Negativtest,
  Resilienz bei Qdrant-/LLM-Ausfall, Datenminimierungstest, Entitätenextraktion, freie
  Slot-Berechnung, Index-Sync-Konsumenten; Web inkl. RBAC über den echten HTTP-Pfad).
- `Tests/CrmWeb.Tests/assistant` (Frontend: State inkl. Resilienz-Fallback, API-Service,
  Chat-Widget a11y/Keyboard, Suggestion-Card Deep-Link).
- `Tests/E2E/tests/assistant.spec.ts` (RBAC-Negativtest echter Backend-Pfad, Resilienz-Szenario
  gemockt) — **geschrieben und strukturell verifiziert** (`npx playwright test --list`), aber
  **noch nicht gegen einen real laufenden Stack ausgeführt**, da in der Implementierungssession
  kein Docker/Aspire-Stack verfügbar war. Vor dem nächsten Produktions-Deployment nachzuverifizieren.
