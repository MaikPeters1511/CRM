# ai-service

KI-gestützter Terminassistent (Bounded Context "Appointment Assistant"). Fachliche Spezifikation:
[`UserStories/ki-terminassistent.openspec.md`](../../UserStories/ki-terminassistent.openspec.md).
Architektur: [`docs/adr/0001-microservices-architektur-crm.md`](../../docs/adr/0001-microservices-architektur-crm.md),
Qdrant-Index-Strategie: [`docs/adr/0003-ai-service-qdrant-index-strategie.md`](../../docs/adr/0003-ai-service-qdrant-index-strategie.md).

`ai-service` legt, ändert oder storniert **niemals** selbst einen Termin — jeder Vorschlag ist
unverbindlich. Die verbindliche Terminanlage erfolgt ausschließlich über das bestehende
`POST /api/appointments` in `appointment-service` inkl. dessen Kollisionsprüfung.

## Starten

```bash
cd Services/ai-service/src/AiService.Web
dotnet run --launch-profile http
```

Port (Dev, Direct): `http://localhost:5289`. Swagger/OpenAPI (nur `Development`):
`http://localhost:5289/swagger`. Üblicherweise wird der Service stattdessen über den
[AppHost](../../AppHost/README.md) mitgestartet (inkl. Qdrant-Container, `ai_db`, RabbitMQ).

## Endpoints

- `POST /api/assistant/chat` — `{ sessionId?, message }` → `{ reply, suggestions }` (Rolle `Employee`, `Admin`)
- `GET /api/assistant/sessions/{sessionId}/history` — Chatverlauf, nur für den eigenen Nutzer (Rolle `Employee`, `Admin`)

## Ablauf pro Chat-Anfrage

1. **Entitätenextraktion** (`ChatEntityExtractor`, Pattern-Matching): Kunde/Leistung(en)/Mitarbeiter/Zeitraum-Hinweis.
2. **Qdrant-Kandidaten-Vorauswahl** (`IAvailabilityIndexClient.FindCandidateServiceIdsAsync`): ordnet
   Freitext-Leistungsbegriffe (inkl. Synonyme/Tippfehler) konkreten Katalog-Leistungen zu — dient
   **ausschließlich** der semantischen Vorauswahl, niemals der harten Verfügbarkeitsentscheidung
   (ADR 0003, Abschnitt 4).
3. **RBAC-Filter** (serverseitig, anhand der `EmployeeId`-Claim des JWT): `Employee` erhält nur
   Vorschläge für die eigene `EmployeeId`; wird im Chat-Text ein anderer Mitarbeiter genannt, wird
   dies abgewiesen, statt die Einschränkung zu umgehen. `Admin` erhält Vorschläge über alle
   qualifizierten Mitarbeiter.
4. **Live-Calls** (Polly-abgesichert: Timeout, 2 Retries mit Backoff, Circuit-Breaker) an
   `employee-service` (Arbeitszeiten/Leistungszuordnung) und `appointment-service`
   (Kollisionsdaten) — die harte Verfügbarkeitsentscheidung basiert ausschließlich hierauf, nie auf
   dem Qdrant-Index.
5. **LLM-Call** (`ILlmClient`, OpenAI oder LMStudio je Konfiguration) formuliert die Antwort aus dem
   bereits minimierten Kontext (abstrakte Zeitfenster/Leistungstyp/Mitarbeiter-Kennung — keine
   vollständige Kundenhistorie, keine internen Notizen).

## Qdrant-Verfügbarkeitsindex

Event-getriebene Aktualisierung (ADR 0003) über Wolverine-Konsumenten für
`AppointmentCreated`/`AppointmentCancelled`/`AppointmentRescheduled` (`appointment-service`),
`EmployeeDeactivated` (`employee-service`) und `ServiceCatalogChanged` (`catalog-service`, neu
eingeführt als Folgeaufgabe dieses ADRs), ergänzt durch einen täglichen, niedrigpriorigen
Konsistenz-Sync-Job (`DailyIndexConsistencySyncHostedService`) als Sicherheitsnetz gegen Drift.
Der Index enthält ausschließlich abstrahierte Service-/Mitarbeiter-Embeddings — keine Kunden- oder
Terminhistorie.

**KNOWN LIMITATION:** Die Konsumenten/der tägliche Sync-Job laufen außerhalb eines eingehenden
HTTP-Request-Kontexts; `AuthHeaderForwardingHandler` kann dort keinen Authorization-Header
weiterreichen (siehe Code-Kommentar). Für den aktuellen, reinen Ableitungsdaten-Scope bewusst
akzeptiert — ein Service-Account-/Client-Credentials-Mechanismus für Hintergrundprozesse ist eine
Folgeaufgabe für `sec-agent`/`architect-agent`.

## Konfiguration (keine Secrets im Code, CLAUDE.md)

| Schlüssel | Beschreibung |
|---|---|
| `LlmProvider` | `OpenAi` (Default) oder `LmStudio` |
| `OpenAi:BaseUrl`, `OpenAi:Model`, `OpenAi:ApiKey` | OpenAI-Anbindung (`ApiKey` über Aspire-Secret-Parameter `openai-api-key`) |
| `LmStudio:BaseUrl`, `LmStudio:Model` | Lokale LMStudio-Anbindung |
| `Qdrant:BaseUrl` | Qdrant-REST-Endpunkt |
| `Services:AppointmentService:BaseUrl` / `EmployeeService` / `CatalogService` / `CustomerService` | Basis-URLs der aufgerufenen Services (Default: Aspire Service Discovery) |

## Datenbank

Eigene, schlanke PostgreSQL-Instanz `ai_db` — **ausschließlich** Chatverlauf/Session-Metadaten
(`ChatSession`/`ChatMessage`). Keine Termin-/Kunden-/Leistungsstammdaten. Migrationen werden beim
Start automatisch angewendet (`DatabaseMigrationHostedService`).

## Tests

`Tests/AiService.Tests` — Domain (`ChatSession`), Application (`HandleChatMessageHandler` inkl.
RBAC-Negativtest, Resilienz bei Qdrant-/LLM-Nichtverfügbarkeit, Datenminimierungstest,
Entitätenextraktion, freie Slot-Berechnung, Index-Sync-Konsumenten) und Web (RBAC über den echten
HTTP-Pfad).
