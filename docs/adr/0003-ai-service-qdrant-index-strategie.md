# 0003. KI-Terminassistent: Qdrant-Index-Aktualisierungsstrategie, Konsistenzmodell und Betrieb

> **Status:** accepted
> **Datum:** 2026-11-01
> **Autor:** architect-agent
> **Betrifft:** `UserStories/ki-terminassistent.openspec.md`
> **Baut auf:** `docs/adr/0001-microservices-architektur-crm.md` (Servicegrenzen, Kommunikationsmuster, Resilienz, interne Clean Architecture, Aspire-Orchestrierung — gilt unverändert)

## Context

`UserStories/ki-terminassistent.openspec.md` (Abschnitt 7, Task 1) sieht einen neuen Microservice `ai-service` vor, der Terminvorschläge per Chat liefert. `ai-service` unterhält dafür einen Qdrant-Vektorindex über abstrahierte Verfügbarkeits-/Leistungs-Daten (Mitarbeiter × Leistung × freie Zeitfenster), der zur semantischen Vorauswahl dient (z.B. Synonymauflösung "Farbe + Schnitt" → Katalog-Leistungen, Kandidaten-Mitarbeiter). Die Spec legt bereits unveränderlich fest, dass die **harte Verfügbarkeitsprüfung stets live** gegen `appointment-service`/`employee-service` erfolgt — Qdrant ist nie alleinige Entscheidungsgrundlage für die finale Slot-Liste. Offen war, **wie und wann** der Index aktualisiert wird, und welche Konsequenzen das für Konsistenz, Betrieb und das `ai-service`-Design hat. Diese Entscheidung war laut Spec vor Implementierungsstart durch den `architect-agent` zu treffen.

Zur Wahl standen:
1. **Event-getrieben**: `ai-service` konsumiert bestehende Integration Events (`AppointmentCreated`/`AppointmentCancelled`/`AppointmentRescheduled` aus `appointment-service`, `EmployeeDeactivated` aus `employee-service`, analog ein Pendant für Leistungszuordnungs-Änderungen) und schreibt den Qdrant-Index inkrementell fort.
2. **Periodischer Batch-Sync**: ein Hintergrund-Job liest in festem Intervall (z.B. alle 5–15 Minuten) den aktuellen Stand aus den drei Quell-Services und baut den Index neu auf bzw. aktualisiert ihn.
3. **On-Demand ohne dauerhaften Index**: pro Chat-Anfrage wird direkt gegen die Quell-Services abgefragt, Qdrant nur für semantische Textsuche auf einem rein statischen Leistungs-Thesaurus (kein Verfügbarkeitsindex).

## Decision

**Es wird Option 1, event-getriebene Index-Aktualisierung, verbindlich umgesetzt.**

### 1. Was im Qdrant-Index liegt — und was nicht

- Der Index enthält ausschließlich **abstrahierte, bereits aggregierte Verfügbarkeits-/Leistungs-Embeddings** je Mitarbeiter: zugeordnete Leistungen (inkl. Synonyme/Beschreibungstext aus `catalog-service` für die semantische Suche), grobe wöchentliche Arbeitszeitfenster aus `employee-service`, sowie — nur als grobe Belegungs-Heuristik, nicht als Einzeltermin-Kopie — belegte Zeitblöcke aus `appointment-service` (z.B. "Montag 10–11 Uhr belegt"), ohne Kunden- oder Leistungsdetails des jeweiligen Termins.
- Der Index enthält **keine** Kundendaten, keine Chat-Inhalte und keine Terminhistorie. Das deckt sich mit Requirement "Datengrundlage für Vorschläge" und "Datenminimierung bei externen LLM-Aufrufen" der Spec.
- Damit bleibt der Index reines **Ableitungsmaterial (derived data)**, kein zusätzlicher Owner von Stammdaten — "Database per Service" (ADR 0001) wird nicht verletzt, da `ai-service` für diese Daten nicht die Quelle der Wahrheit ist, sondern ausschließlich `appointment-service`/`employee-service`/`catalog-service`.

### 2. Aktualisierungsmechanismus: Event-getrieben

`ai-service` registriert Wolverine-Konsumenten für folgende, bereits bestehende Integration Events (keine neuen Events in den Quell-Services nötig, bis auf einen Fall):

| Event | Quelle | Wirkung auf den Qdrant-Index |
|---|---|---|
| `AppointmentCreated` | `appointment-service` | Entsprechendes Zeitfenster beim betroffenen Mitarbeiter als belegt markieren/Embedding aktualisieren |
| `AppointmentCancelled` | `appointment-service` | Zeitfenster wieder als frei markieren |
| `AppointmentRescheduled` | `appointment-service` | Altes Zeitfenster freigeben, neues als belegt markieren |
| `EmployeeDeactivated` | `employee-service` | Mitarbeiter aus dem Index entfernen (keine Vorschläge mehr für diese `EmployeeId`) |
| `ServiceCatalogChanged` *(neu, siehe unten)* | `catalog-service` | Leistungs-Embedding (Name/Beschreibung/aktiv-Status) aktualisieren |

Für `catalog-service` existiert laut `UserStories/specs/service-catalog.openspec.md` bislang **kein** Integration Event beim Anlegen/Ändern/Deaktivieren einer Leistung (bewusst, da bis dato kein Konsument benötigt wurde — siehe Design-Abschnitt der archivierten Leistungskatalog-Change-Spec: "aktuell kein Konsument, daher ohne Event-Publishing zulässig, kein Gold-Plating"). Mit `ai-service` entsteht erstmals ein Konsument. Daher gilt als Folgeentscheidung dieses ADR: `catalog-service` veröffentlicht künftig ein asynchrones Integration Event `ServiceCatalogChanged` (bei Anlegen/Bearbeiten/Deaktivieren einer Leistung), analog den bestehenden Mustern aus ADR 0001 Abschnitt 2. Dies ist eine minimale, additive Erweiterung von `catalog-service` (kein Bruch bestehender Requirements) und wird als Folgeaufgabe an `backend-agent` übergeben.

Arbeitszeitänderungen in `employee-service` (z.B. geänderte Schichtzeiten) lösen **keinen** dedizierten Event-Konsum aus — diese ändern sich selten und werden stattdessen durch den in Abschnitt 3 beschriebenen täglichen Konsistenz-Sync abgedeckt, um nicht für jede Stammdatenänderung ein neues Event einzuführen (kein Gold-Plating).

### 3. Ergänzender täglicher Konsistenz-Sync (Reparatur-Mechanismus, kein Primärpfad)

Zusätzlich zur Event-Verarbeitung läuft ein **täglicher, niedrigpriorer Hintergrund-Job** (Wolverine Scheduled Job, außerhalb der Geschäftszeiten), der den Index gegen den aktuellen Stand der drei Quell-Services vollständig neu abgleicht. Begründung: Event-Konsumenten sind idempotent und zuverlässig (Wolverine Inbox/Outbox, ADR 0001 Abschnitt 3), aber kein verteiltes System ist gegen jede Lücke immun (z.B. ein Event, das vor Einführung dieses Features bereits verarbeitet wurde, manuelle DB-Korrekturen an Quell-Services ohne Event, oder ein Edge Case bei der `EmployeeDeactivated`-Verarbeitung). Der tägliche Sync ist bewusst **kein Ersatz** für die Event-Verarbeitung (das wäre Option 2 und würde unnötige Latenz zwischen Terminänderung und Indexaktualität von bis zu 24h einführen), sondern ein günstiges Sicherheitsnetz gegen Drift.

### 4. Konsistenzmodell: Qdrant ist immer nur Vorauswahl, nie Entscheidungsgrundlage

Dieses ADR bestätigt und präzisiert die in der Spec bereits getroffene Festlegung: Der Qdrant-Index darf **ausschließlich** zur Kandidaten-Vorauswahl (semantische Zuordnung von Nutzertext zu Leistungen/Mitarbeitern, grobe Eingrenzung möglicher Zeiträume) verwendet werden. Jeder dem Nutzer letztlich angezeigte Slot-Vorschlag **muss** unmittelbar vor der Anzeige durch einen synchronen, Polly-abgesicherten Live-Call gegen `appointment-service`/`employee-service` verifiziert werden (wie in der Spec, Requirement "Datengrundlage für Vorschläge", bereits vorgesehen). Damit ist die maximale Veraltung des Qdrant-Index — ob durch Event-Verzögerung oder die 24h-Lücke zwischen Syncs — fachlich irrelevant für die Korrektheit der Vorschläge; sie wirkt sich höchstens auf die **Trefferqualität** der Vorauswahl aus (ein inzwischen belegter Slot wird evtl. kurz vorgeschlagen und dann durch die Live-Prüfung aussortiert), nie auf eine fehlerhafte Terminanlage. Dies ist bereits durch Requirement "Vorgeschlagener Slot ist inzwischen belegt" der Spec abgefangen.

### 5. Betrieb (Docker/Aspire)

- Qdrant wird als eigene Container-Ressource im zentralen `AppHost`-Projekt registriert (analog zu den PostgreSQL-Instanzen pro Service, ADR 0001 Abschnitt 7), referenziert ausschließlich von `ai-service`.
- Der Qdrant-Index wird beim ersten Start von `ai-service` einmalig aus dem aktuellen Stand aller drei Quell-Services befüllt (Initial-Sync, derselbe Code-Pfad wie der tägliche Konsistenz-Sync aus Abschnitt 3).
- Qdrant-Daten sind reine Ableitungsdaten ohne Backup-Pflicht im strengen Sinn (ein Verlust des Volumes erfordert nur einen Neuaufbau via Initial-Sync, keinen Datenverlust an Quelldaten) — dies vereinfacht den Betrieb gegenüber einer klassischen Owner-Datenbank.
- Resilienz bei Qdrant-Nichterreichbarkeit: siehe Requirement "Resilienz bei Nichtverfügbarkeit der KI-API"/Scenario "Qdrant-Index nicht erreichbar" der Spec — klare Fehlermeldung statt Vorschläge auf unsicherer Basis, kein Fallback auf ungeprüfte Daten.

### 6. Implikationen für das `ai-service`-Design

- `ai-service` benötigt zusätzlich zur bereits in der Spec vorgesehenen Infrastruktur (Qdrant-Client, `ILlmClient`-Abstraktion, typisierte HTTP-Clients zu den drei Services) Wolverine-Event-Konsumenten für die vier/fünf oben genannten Events sowie einen Scheduled-Job für den täglichen Sync — beides in der Infrastructure-Schicht, Trigger/Orchestrierung der Indexaktualisierung in der Application-Schicht (kein Domain-Layer-Zugriff auf Qdrant, analog zur in ADR 0001 Abschnitt 6 festgelegten Schichtentrennung).
- Idempotenz der Event-Verarbeitung (Wolverine Inbox, Deduplizierung über Event-Id) ist verbindlich, da sowohl Event-Konsum als auch täglicher Sync denselben Indexeintrag schreiben können (Reihenfolge-unabhängige, letztgültige Aktualisierung pro Mitarbeiter/Zeitfenster).

## Consequences

**Positiv:**
- Folgt dem bereits etablierten asynchronen Kommunikationsmuster aus ADR 0001 — keine neue Kommunikationsart, geringe Lernkurve für `backend-agent`.
- Niedrige Latenz zwischen Terminänderung und Indexaktualität (Sekunden statt Minuten/Stunden bei reinem Batch-Sync), ohne Polling-Overhead auf die drei Kern-Services.
- Fehlerfreiheit der Terminanlage bleibt unabhängig von der Indexaktualität, da Qdrant nie die finale Entscheidungsgrundlage ist (Abschnitt 4).
- Täglicher Sync als Sicherheitsnetz hält den operativen Aufwand bei Drift gering, ohne ihn zum Primärpfad zu machen.

**Negativ / Trade-offs:**
- `catalog-service` muss um ein neues Integration Event (`ServiceCatalogChanged`) erweitert werden — zusätzlicher, bislang nicht vorgesehener Aufwand gegenüber dem bestehenden, eventlosen Design.
- Zusätzliche Betriebskomponente (Qdrant-Container) und zusätzlicher Scheduled Job erhöhen die Komplexität von `ai-service` gegenüber einem rein synchronen On-Demand-Ansatz (Option 3) — bewusst akzeptiert, da die semantische Vorauswahl (Synonyme, Freitext-Interpretation) ohne Vektorindex fachlich nicht sinnvoll umsetzbar wäre.
- Idempotenz-Handling zwischen Event-Konsum und täglichem Sync muss sauber implementiert werden (siehe Abschnitt 6), sonst Risiko von Race-Conditions zwischen beiden Schreibpfaden.

**Folgeaufgaben:**
- `backend-agent`: Integration Event `ServiceCatalogChanged` in `catalog-service` einführen und veröffentlichen (Anlegen/Bearbeiten/Deaktivieren einer Leistung); `UserStories/specs/service-catalog.openspec.md` entsprechend um dieses Event ergänzen (MODIFIED Requirement).
- `backend-agent`: Wolverine-Event-Konsumenten in `ai-service` für `AppointmentCreated`/`AppointmentCancelled`/`AppointmentRescheduled`/`EmployeeDeactivated`/`ServiceCatalogChanged` sowie den täglichen Konsistenz-Sync-Job implementieren, inkl. Idempotenz (Inbox-Pattern).
- `devops-agent`: Qdrant-Container-Ressource im `AppHost` registrieren, referenziert von `ai-service`.
- `po-agent`: Task-Liste in `UserStories/ki-terminassistent.openspec.md` (Task 1) als erledigt markieren und auf dieses ADR verweisen.
- `architect-agent`: Vor Archivierung der Change-Spec bestätigen, dass Umsetzung (Event-Konsum, täglicher Sync, Konsistenzmodell) mit diesem ADR übereinstimmt.
