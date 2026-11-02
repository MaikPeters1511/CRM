# Change: Admin-Einstellungsseite KI-Assistent — Backend-Konfigurations-API

> **Status:** implemented (noch nicht archiviert)

## 1. Proposal (Why & What)
- **Why:** Die Konfiguration des KI-Terminassistenten (`ai-service`, siehe `UserStories/specs/appointment-assistant.openspec.md`, `docs/adr/0003-ai-service-qdrant-index-strategie.md`) — LLM-Provider, Modell, API-Key, Qdrant-URL — ist ausschließlich statisch über `appsettings`/Secrets gesetzt. Ein Admin kann weder zur Laufzeit einsehen, welcher Provider/welches Modell aktiv ist, ob überhaupt ein API-Key gesetzt ist, wann der Qdrant-Verfügbarkeitsindex zuletzt synchronisiert wurde, noch den Assistenten kurzfristig deaktivieren (z.B. bei Störungen/Kostenkontrolle) oder einen sofortigen Index-Sync anstoßen, ohne einen Deployment-Vorgang anzustoßen. Das erschwert Betrieb und Fehlerdiagnose.
- **What Changes:**
  - Neuer, admin-only API-Bereich in `ai-service`: `GET /api/assistant/settings`, `PUT /api/assistant/settings`, `POST /api/assistant/settings/sync-now`.
  - Neue, schlanke Persistenz `assistant_settings` (Singleton-Zeile) in der bestehenden `ai_db` für den Enabled-Flag und die zuletzt beobachtete Synchronisationszeit sowie die zuletzt bekannten Provider-/Modell-/Qdrant-URL-Werte (siehe Design, Abschnitt 4 — Entscheidung zur Laufzeitänderung ohne Neustart).
  - Der bestehende Chat-Endpunkt (`POST /api/assistant/chat`) respektiert den Enabled-Flag und liefert bei deaktiviertem Assistenten `503 Service Unavailable` statt eine Chat-Antwort zu generieren.
  - Der bestehende tägliche Konsistenz-Sync-Job (`IndexConsistencySyncHandler`, ADR 0003 Abschnitt 3) schreibt nach jedem Lauf (ob täglich getriggert oder on-demand per `sync-now`) einen Zeitstempel in `assistant_settings`.
  - **Explizit kein** Rückgabe-Pfad für den tatsächlichen API-Key im Klartext — nur maskiert (z.B. `sk-••••1234`) bzw. ob ein Key gesetzt ist. Ein neuer API-Key wird über `PUT` **nicht** entgegengenommen und **nicht** in der Datenbank gespeichert (bleibt Secret/Environment-Variable, siehe `CLAUDE.md` Abschnitt Sicherheit).
- **Impact:** Nur `ai-service` ist betroffen (neuer Endpunkt-Bereich + eine neue Tabelle in `ai_db`, neue EF-Core-Migration). Kein neuer Microservice, keine Änderung an `appointment-service`/`employee-service`/`catalog-service`/`customer-service`. Frontend (`Clients/crm-web`, Admin-Seite "KI-Assistent") ist **nicht** Teil dieser Change-Spec — konsumiert die hier beschriebene API in einer separaten Change.

## 2. User Stories
- Als Admin möchte ich sehen, welcher LLM-Provider/welches Modell aktuell aktiv ist und ob ein API-Key gesetzt ist, damit ich die Konfiguration ohne Serverzugriff nachvollziehen kann.
- Als Admin möchte ich sehen, wann der Qdrant-Verfügbarkeitsindex zuletzt synchronisiert wurde, damit ich eine mögliche Datenveraltung einschätzen kann.
- Als Admin möchte ich den Assistenten bei Bedarf deaktivieren können, ohne einen Neustart/Deployment abzuwarten, damit ich bei Störungen oder zur Kostenkontrolle schnell reagieren kann.
- Als Admin möchte ich einen sofortigen Index-Sync anstoßen können, statt auf den nächsten täglichen Lauf zu warten, damit ich nach größeren Stammdatenänderungen zeitnah einen konsistenten Index habe.
- Als Admin möchte ich, dass der tatsächliche API-Key niemals im Klartext über die API zurückgegeben wird, damit ein Secret nicht versehentlich offengelegt wird (OWASP A02/A05).

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Admin-Only Einsicht in die KI-Assistent-Konfiguration
Das System SHALL einem authentifizierten Nutzer mit Rolle `Admin` über `GET /api/assistant/settings` die aktuell wirksame Konfiguration des KI-Assistenten offenlegen: Enabled-Flag, LLM-Provider, Modellname, Qdrant-Server-URL, ob ein API-Key gesetzt ist (maskiert, niemals im Klartext), und den Zeitpunkt der letzten Qdrant-Indexsynchronisation. Nutzer ohne Rolle `Admin` SHALL mit `403 Forbidden` abgewiesen werden.

##### Scenario: Admin ruft die Einstellungen ab
- **GIVEN** ein Nutzer mit Rolle `Admin` ist authentifiziert und ein API-Key ist konfiguriert
- **WHEN** er `GET /api/assistant/settings` aufruft
- **THEN** liefert die Antwort `200 OK` mit Provider, Modellname, Qdrant-URL, Enabled-Flag, einer maskierten Darstellung des API-Keys (z.B. `sk-••••1234`) und dem Zeitpunkt der letzten Indexsynchronisation (oder `null`, falls noch nie synchronisiert)

##### Scenario: Kein API-Key konfiguriert
- **GIVEN** für den aktiven Provider ist kein API-Key gesetzt (z.B. LMStudio lokal ohne Key)
- **WHEN** ein Admin `GET /api/assistant/settings` aufruft
- **THEN** zeigt die Antwort an, dass kein API-Key gesetzt ist, ohne einen leeren String mit einem gesetzten Key zu verwechseln

##### Scenario: Nicht-Admin versucht Zugriff
- **GIVEN** ein Nutzer mit Rolle `Employee` ist authentifiziert
- **WHEN** er `GET /api/assistant/settings` aufruft
- **THEN** liefert die Antwort `403 Forbidden`

#### Requirement: API-Key wird niemals im Klartext zurückgegeben
Das System SHALL unter keinen Umständen — weder bei `GET` noch nach einem `PUT` — den tatsächlichen API-Key-Wert im Klartext in einer API-Antwort ausliefern.

##### Scenario: Antwort enthält keinen Klartext-Key
- **GIVEN** ein API-Key ist konfiguriert
- **WHEN** ein Admin die Einstellungen abruft
- **THEN** enthält die Antwort ausschließlich eine maskierte Darstellung (Präfix + letzte 4 Zeichen) oder ein Boolean, niemals den vollständigen Key-String

#### Requirement: Admin kann Enabled-Flag, Provider, Modell und Qdrant-URL ändern
Das System SHALL einem Admin über `PUT /api/assistant/settings` erlauben, das Enabled-Flag, den Provider (OpenAI/LMStudio), den Modellnamen und die Qdrant-Server-URL zu ändern. Das Enabled-Flag SHALL ohne Neustart von `ai-service` sofort wirksam werden. Änderungen an Provider, Modellname oder Qdrant-URL SHALL persistiert werden, SHALL aber erst nach einem Neustart von `ai-service` tatsächlich wirksam werden (siehe Design, Abschnitt 4); die Antwort SHALL in diesem Fall klar anzeigen, dass ein Neustart erforderlich ist. Ein API-Key SHALL über diesen Endpunkt **nicht** entgegengenommen werden.

##### Scenario: Admin deaktiviert den Assistenten
- **GIVEN** der Assistent ist aktuell aktiviert
- **WHEN** ein Admin `PUT /api/assistant/settings` mit `enabled: false` (sonst unverändert) aufruft
- **THEN** liefert die Antwort `200 OK` mit `enabled: false`, und ein unmittelbar danach gesendeter Chat-Request (`POST /api/assistant/chat`) wird mit `503 Service Unavailable` abgewiesen, ohne dass `ai-service` neu gestartet wurde

##### Scenario: Admin ändert den Provider
- **GIVEN** der Assistent läuft aktuell mit Provider `OpenAi`
- **WHEN** ein Admin `PUT /api/assistant/settings` mit `provider: "LmStudio"` aufruft
- **THEN** liefert die Antwort `200 OK` mit den persistierten neuen Werten und `restartRequired: true` samt verständlichem Hinweistext, dass die Provider-Umstellung erst nach einem Neustart von `ai-service` aktiv wird

##### Scenario: PUT-Body enthält einen API-Key
- **GIVEN** ein Admin sendet `PUT /api/assistant/settings` mit einem zusätzlichen Feld für einen API-Key
- **WHEN** die Anfrage verarbeitet wird
- **THEN** wird dieses Feld ignoriert (kein Schreiben in die Datenbank), und die Antwort dokumentiert, dass der API-Key ausschließlich über Secret/Environment-Variable gesetzt werden kann

##### Scenario: Nicht-Admin versucht Änderung
- **GIVEN** ein Nutzer mit Rolle `Employee` ist authentifiziert
- **WHEN** er `PUT /api/assistant/settings` aufruft
- **THEN** liefert die Antwort `403 Forbidden`, und es werden keine Einstellungen geändert

#### Requirement: Admin kann einen sofortigen Qdrant-Index-Sync anstoßen
Das System SHALL einem Admin über `POST /api/assistant/settings/sync-now` erlauben, den bestehenden Qdrant-Konsistenz-Sync-Job (ADR 0003, Abschnitt 3) sofort auszuführen, statt auf den nächsten täglichen Lauf zu warten. Die Antwort SHALL den Zeitpunkt des vorherigen Laufs (falls vorhanden) sowie den Zeitpunkt des soeben abgeschlossenen Laufs enthalten.

##### Scenario: Admin stößt Sofort-Sync an
- **GIVEN** der letzte Konsistenz-Sync lief vor 20 Stunden
- **WHEN** ein Admin `POST /api/assistant/settings/sync-now` aufruft
- **THEN** liefert die Antwort `200 OK` mit dem Zeitpunkt des vorherigen Laufs (vor 20 Stunden) und dem Zeitpunkt des soeben abgeschlossenen Laufs (jetzt), und ein nachfolgender `GET /api/assistant/settings`-Aufruf zeigt den aktualisierten Zeitpunkt der letzten Synchronisation

##### Scenario: Nicht-Admin versucht Sofort-Sync
- **GIVEN** ein Nutzer mit Rolle `Employee` ist authentifiziert
- **WHEN** er `POST /api/assistant/settings/sync-now` aufruft
- **THEN** liefert die Antwort `403 Forbidden`, und es wird kein Sync ausgelöst

## 4. Design

**Persistenz- und Laufzeitänderungs-Entscheidung (architect-agent):** Siehe `docs/adr/0004-ki-assistent-einstellungen-laufzeit.md`. Zusammenfassung: Eine vollständig neustartfreie Laufzeitänderung von Provider/Modell/Qdrant-URL ist mit dem bestehenden Design (Provider-Auswahl erfolgt einmalig beim Start über `AddAiServiceInfrastructure`/`IConfiguration`, HTTP-Clients werden mit fester `BaseAddress`/Headers beim Start verdrahtet) ohne signifikanten Mehraufwand (`IOptionsMonitor`+dynamische `HttpClient`-Neuerstellung je Request oder ein Re-Resolve-Mechanismus) nicht sauber möglich und würde eine Komplexität einführen, die für dieses Feature nicht gerechtfertigt ist (kein Gold-Plating). Entscheidung: Eine neue, singuläre Tabelle `assistant_settings` in `ai_db` persistiert `Enabled`, `Provider`, `Model`, `QdrantBaseUrl`, `LastIndexSyncAt` und `RestartRequired`. `Enabled` wird bei jedem Chat-Request live aus der Datenbank gelesen (kein Neustart nötig). Provider/Modell/Qdrant-URL werden zwar sofort persistiert und per `GET` sichtbar, wirken sich aber erst nach einem Neustart aus; beim Start liest `ai-service` die tatsächlich aktiven Werte aus `IConfiguration` (unverändertes Verhalten) und schreibt sie zur Konsistenz in `assistant_settings` zurück (`RestartRequired = false`).

## 5. Tasks
- [x] OpenSpec-Change-Spec erstellt (dieses Dokument).
- [x] ADR 0004 (architect-agent): Persistenz-/Laufzeitänderungs-Entscheidung dokumentieren.
- [x] `backend-agent`: EF-Core-Entity/Migration `assistant_settings`, Repository-Abstraktion, Startup-Seed-Hosted-Service.
- [x] `backend-agent`: Application-Layer (Query `GetAssistantSettings`, Command `UpdateAssistantSettings`, Command `TriggerIndexSync`), TDD.
- [x] `backend-agent`: `AssistantSettingsController` (`GET`/`PUT`/`api/assistant/settings`, `POST /api/assistant/settings/sync-now`), `[Authorize(Roles = "Admin")]`.
- [x] `backend-agent`: Enabled-Flag-Prüfung in `POST /api/assistant/chat` (503 bei deaktiviertem Assistenten).
- [x] `backend-agent`: `IndexConsistencySyncHandler` schreibt `LastIndexSyncAt` nach jedem Lauf.
- [x] Lokale Verifikation: Build + Tests grün (56/56, `Tests/AiService.Tests`).

**Bewusst nicht Teil dieser Session:** Frontend-Integration (separate Session), README-Betriebsdokumentation (Folgeaufgabe `doc-agent`, siehe ADR 0004 Abschnitt "Consequences").
