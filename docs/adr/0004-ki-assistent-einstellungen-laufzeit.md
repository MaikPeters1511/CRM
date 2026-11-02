# 0004. KI-Assistent Admin-Einstellungen: Persistenz und Laufzeitänderung ohne Neustart

> **Status:** accepted
> **Datum:** 2026-11-02
> **Autor:** architect-agent
> **Betrifft:** `UserStories/ki-einstellungen-backend.openspec.md`
> **Baut auf:** `docs/adr/0003-ai-service-qdrant-index-strategie.md` (Qdrant-Index/Konsistenz-Sync, gilt unverändert)

## Context

`UserStories/ki-einstellungen-backend.openspec.md` verlangt eine Admin-API (`GET`/`PUT /api/assistant/settings`), über die Enabled-Flag, LLM-Provider, Modellname und Qdrant-URL einsehbar und änderbar sind. Aktuell werden diese Werte ausschließlich einmalig beim Start in `AiService.Infrastructure.DependencyInjection.AddAiServiceInfrastructure` aus `IConfiguration` gelesen und zu konkreten, fest verdrahteten Objekten:

- `ILlmClient` wird als `Scoped`-Service mit einem bereits beim Start konfigurierten `HttpClient` (feste `BaseAddress`, bei OpenAI fester `Authorization`-Header) registriert (`AddLlmClient`).
- `IAvailabilityIndexClient` (Qdrant) wird ebenso mit fester `BaseAddress` beim Start registriert (`AddQdrantClient`).

Die Spec erlaubt explizit die Vereinfachung, dass nur neustartfrei wirksame Felder (z.B. Enabled) tatsächlich sofort wirken dürfen, wenn eine saubere neustartfreie Änderung der übrigen Felder architektonisch nicht sinnvoll ist. Zu entscheiden war: (a) wo die Einstellungen persistiert werden, und (b) ob/wie Provider/Modell/Qdrant-URL ohne Neustart wirksam gemacht werden.

Optionen für (b):
1. **`IOptionsMonitor<T>` + dynamische Re-Konfiguration der `HttpClient`-Instanzen pro Aufruf** (z.B. `IAvailabilityIndexClient`/`ILlmClient` als Factory statt fixer DI-Registrierung, `BaseAddress`/Auth-Header bei jedem Request aus den aktuellen DB-Werten lesen).
2. **Kein neustartfreier Wechsel für Provider/Modell/Qdrant-URL** — nur das Enabled-Flag wirkt sofort (live aus der DB gelesen); Änderungen an den übrigen Feldern werden persistiert und als "wirksam nach Neustart" gekennzeichnet.

## Decision

**Es wird Option 2 umgesetzt.**

### Begründung

- Die aktuelle Infrastruktur verdrahtet `ILlmClient`/`IAvailabilityIndexClient` bewusst einmalig beim Start (Requirement "Konfigurierbare LLM-Anbindung (OpenAI/LMStudio)" der ursprünglichen Spec, Kommentar in `DependencyInjection.cs`: "Auswahl ausschließlich über Konfiguration, keine Codeänderung in `HandleChatMessageHandler`"). Eine neustartfreie Umschaltung des Providers würde bedeuten, bei **jedem** Chat-/Sync-Request den aktuellen DB-Wert zu lesen und ggf. einen neuen `HttpClient` mit anderer `BaseAddress`/anderem Auth-Header zu erzeugen (oder einen Pool vorzuhalten) — inkl. Re-Validierung der Resilienz-Pipeline (Polly) pro Provider. Das ist technisch machbar, aber für ein Admin-Feature mit seltener Änderungsfrequenz (Provider-Wechsel ist ein administrativer Ausnahmefall, kein Normalbetrieb) unverhältnismäßiger Mehraufwand und zusätzliche Fehlerquelle (z.B. Credential-Leak zwischen Requests bei falscher Scoped/Singleton-Nutzung) — **Gold-Plating im Sinne von `CLAUDE.md`**.
- Das Enabled-Flag hat keine vergleichbare Komplexität: Es ist ein reiner Gate-Check (boolean) vor der eigentlichen Chat-Verarbeitung, ohne Auswirkung auf bereits verdrahtete `HttpClient`-Instanzen. Ein `Scoped`/`Transient` Repository-Read pro Request ist hier unproblematisch (gleiche Kostenklasse wie der bestehende `IChatSessionRepository`-Zugriff).
- Damit bleibt die bestehende, bewusst einfache DI-Verdrahtung (ADR 0003 Abschnitt 6) unangetastet; es wird keine neue Abstraktionsebene (Factory/`IOptionsMonitor`-Wrapper) für `ILlmClient`/`IAvailabilityIndexClient` eingeführt.

### Persistenz

Neue Tabelle `assistant_settings` in der bestehenden `ai_db` (keine neue Datenbank — `ai_db` ist laut `AiDbContext`-Dokumentation ohnehin für "Session-Metadaten" des Assistenten vorgesehen, Einstellungen sind davon nicht grundverschieden). Singleton-Zeile (feste `Id`), Spalten:

| Spalte | Typ | Bedeutung |
|---|---|---|
| `Id` | `Guid` (fest) | Singleton-Schlüssel |
| `Enabled` | `bool` | Live gelesen vor jeder Chat-Verarbeitung |
| `Provider` | `string` | Zuletzt vom Admin gewählter/beim Start aktiver Provider (Anzeige + "gewünschter Zielzustand") |
| `Model` | `string` | s.o. |
| `QdrantBaseUrl` | `string` | s.o. |
| `RestartRequired` | `bool` | `true`, solange der per `PUT` gesetzte Provider/Modell/Qdrant-URL-Wert noch nicht durch einen Neustart aktiv gemacht wurde |
| `LastIndexSyncAt` | `DateTimeOffset?` | Von `IndexConsistencySyncHandler` nach jedem Lauf (täglich oder `sync-now`) aktualisiert |
| `UpdatedAt` | `DateTimeOffset` | Zeitpunkt der letzten `PUT`-Änderung |

Beim Start von `ai-service` (neuer `AssistantSettingsSeedHostedService`, analog `DatabaseMigrationHostedService`) wird die Zeile angelegt (falls nicht vorhanden, mit den aktuell aus `IConfiguration` gelesenen Werten und `Enabled = true`) bzw. `Provider`/`Model`/`QdrantBaseUrl` auf die tatsächlich jetzt aktiven, aus `IConfiguration` gelesenen Werte zurückgesetzt und `RestartRequired = false` gesetzt — der Neustart hat die vorher per `PUT` gesetzten Werte ja inzwischen tatsächlich übernommen (sie sind identisch mit dem, was beim Neustart aus `appsettings`/Secrets gelesen wurde, sofern der Betreiber die Konfiguration entsprechend aktualisiert hat — das Nachziehen der tatsächlichen `appsettings`/Secret-Werte nach einem von `PUT` ausgelösten "Neustart erforderlich" bleibt ein manueller Betriebsschritt, s. Consequences). `Enabled` bleibt beim Neustart unverändert (Admin-Entscheidung überlebt Neustarts).

Der API-Key wird **nicht** in dieser Tabelle gespeichert (Requirement "API-Key wird niemals im Klartext zurückgegeben"); `GET` liest den Key ausschließlich aus `IConfiguration`/Environment zur Maskierung, `PUT` nimmt kein Key-Feld entgegen.

## Consequences

**Positiv:**
- Keine neue Abstraktionsebene für `ILlmClient`/`IAvailabilityIndexClient`, minimaler Eingriff in bestehenden, funktionierenden Code.
- Enabled-Flag wirkt echt sofort — deckt den in der Praxis wichtigsten Betriebsfall (Assistent kurzfristig abschalten) vollständig ab.
- `assistant_settings` ist einfache, gut testbare Infrastruktur (ein Repository, ein EF-Core-Entity), passt zum bestehenden `ai_db`-Zuschnitt.

**Negativ / Trade-offs:**
- Ein per `PUT` geänderter Provider/Modell/Qdrant-URL wird in `assistant_settings` sichtbar ("gewünschter Zielzustand"), ist aber bis zum nächsten Neustart **nicht** der tatsächlich aktive Wert — ein Admin, der `GET` aufruft, sieht ggf. einen Wert, der noch nicht wirksam ist (`restartRequired: true` macht das aber explizit und unmissverständlich sichtbar).
- Der Neustart selbst übernimmt die neuen Werte nur, wenn der Betreiber zusätzlich `appsettings`/Secrets/Environment entsprechend nachzieht — die DB-Werte steuern die tatsächliche DI-Verdrahtung **nicht**. Das ist eine bewusste Einschränkung dieser Iteration; ein vollständiger "DB ist Source of Truth auch für den Neustart"-Mechanismus (DB vor DI-Aufbau lesen) ist nicht Teil dieser Entscheidung und würde eine separate Change-Spec erfordern, falls künftig benötigt.

**Folgeaufgaben:**
- `backend-agent`: Umsetzung gemäß `UserStories/ki-einstellungen-backend.openspec.md`, Abschnitt 5 (Tasks).
- `doc-agent`: README/Betriebsdokumentation von `ai-service` um den Hinweis ergänzen, dass eine per Admin-API vorgenommene Provider-/Modell-/URL-Änderung zusätzlich in `appsettings`/Secrets nachgezogen werden muss, damit sie nach einem Neustart tatsächlich aktiv wird.
