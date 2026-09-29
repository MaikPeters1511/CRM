# 0001. Microservices-Architektur für das CRM (Servicegrenzen, Kommunikation, Konsistenz, Gateway, Resilienz)

> **Status:** accepted
> **Datum:** 2026-09-29
> **Autor:** architect-agent
> **Betrifft:** `UserStories/kundenverwaltung.openspec.md`, `UserStories/mitarbeiterverwaltung.openspec.md`, `UserStories/terminverwaltung.openspec.md`

## Context

Das CRM wird als Microservices-Architektur mit drei fachlichen Bounded Contexts umgesetzt: `customer-service`, `employee-service` und `appointment-service`. Die zugehörigen Change-Specs (PO-Agent) enthalten bereits vorläufige Annahmen zur Inter-Service-Kommunikation (synchrone REST-Calls für Verfügbarkeits- und Kundenvalidierung, Integration Events `AppointmentCreated`/`AppointmentCancelled`/`AppointmentRescheduled`, `CustomerDeactivated`, `EmployeeDeactivated`), markieren diese jedoch ausdrücklich als vom Architect-Agent final zu entscheiden. Es fehlt eine verbindliche, für alle drei Services geltende Festlegung zu:

- Servicegrenzen und Verantwortlichkeiten,
- welche Interaktionen synchron vs. asynchron erfolgen,
- Datenkonsistenzstrategie bei Cross-Service-Validierung,
- Zugriffspfad des Angular-Frontends auf drei separate Backends,
- Resilienz-Anforderungen für Cross-Service-Calls,
- Einhaltung der intern vorgeschriebenen Clean Architecture (`CLAUDE.md`) innerhalb jedes Microservice,
- Orchestrierung/Deployment über .NET Aspire.

Diese Entscheidung ist Voraussetzung für den Implementierungsstart aller drei Changes (siehe jeweils Abschnitt 1 "Impact" und Abschnitt 7 "Design").

## Decision

### 1. Servicegrenzen und Verantwortlichkeiten (Bounded Contexts)

| Service | Bounded Context | Verantwortlichkeit | Eigene DB |
|---|---|---|---|
| `customer-service` | Customer Management | Kundenstammdaten, Suche/Filterung, Aktivierungsstatus. Kennt **keine** Termine (Terminreferenzen werden nicht per DB-Join, sondern per Call an `appointment-service` aufgelöst). | `customer_db` (PostgreSQL) |
| `employee-service` | Employee Management | Mitarbeiterstammdaten, Rollen (RBAC-Basisdaten), Leistungen/Qualifikationen, Arbeitszeiten, Abwesenheiten, Verfügbarkeitsberechnung (Arbeitszeit − Abwesenheit − bestehende Termine). | `employee_db` (PostgreSQL) |
| `appointment-service` | Appointment Scheduling | Terminlebenszyklus (Anlegen/Verschieben/Stornieren), Kollisionsprüfung innerhalb eigener Termine, Kalenderansicht, Erinnerungs-Job. Hält nur `CustomerId`/`EmployeeId` als Fremdreferenz (Guid), keine Kopie der Stammdaten außer für Anzeige-Caching. | `appointment_db` (PostgreSQL) |

Jeder Service ist alleiniger Owner seiner Daten ("Database per Service"). Kein Service greift direkt auf die Datenbank eines anderen Service zu; jeglicher Zugriff läuft ausschließlich über die öffentliche API bzw. Integration Events des jeweiligen Owners.

### 2. Kommunikationsmuster: synchron vs. asynchron

**Grundsatz:** Synchrone REST-Calls (über .NET Aspire Service Discovery) werden nur dort eingesetzt, wo eine Entscheidung *im selben Request* eine aktuelle, verbindliche Antwort eines anderen Service benötigt (Validierung vor einem Schreibvorgang). Alle reinen Zustandsänderungs-Benachrichtigungen an nachgelagerte, nicht blockierende Konsumenten laufen als asynchrone Integration Events über einen Message Broker (Wolverine Messaging, Transport RabbitMQ, orchestriert via Aspire).

| Interaktion | Muster | Begründung |
|---|---|---|
| `appointment-service` → `employee-service`: `GET /api/employees/{id}/availability` vor Anlage/Verschiebung eines Termins | **Synchron (REST)** | Kollisionsprüfung erfordert den aktuellen Verfügbarkeitsstand zum Zeitpunkt der Buchung; ein veralteter/eventuell asynchron nachgezogener Stand würde Doppelbuchungen ermöglichen. Harte Konsistenzanforderung an dieser Stelle rechtfertigt den synchronen Call trotz Kopplung. |
| `appointment-service` → `customer-service`: `GET /api/customers/{id}` vor Terminanlage (Existenz/Aktivstatus) | **Synchron (REST)** | Ein Termin darf nicht für einen nicht existierenden oder bereits deaktivierten Kunden angelegt werden; dies ist eine Vorbedingung des Schreibvorgangs, keine Benachrichtigung. Da das Ergebnis unmittelbar die Annahme/Ablehnung des Requests bestimmt, ist ein synchroner Call fachlich erforderlich. |
| `employee-service` → `appointment-service`: Abgleich bestehender Termine bei Abwesenheits-/Arbeitszeitkonflikt | **Synchron (REST)** | Analog: Die Konfliktprüfung beim Eintragen einer Abwesenheit muss den aktuellen Terminstand kennen, um dem Admin/Mitarbeiter sofort eine Konfliktwarnung anzuzeigen (Requirement "Urlaub kollidiert mit bestehendem Termin"). |
| `customer-service` → alle Konsumenten: `CustomerDeactivated` | **Asynchron (Integration Event)** | Die Deaktivierung eines Kunden ist ein abgeschlossener Fakt im Owner-Service; nachgelagerte Services (`appointment-service`) müssen reagieren (z.B. zukünftige Termine markieren/Reporting), aber die Deaktivierung selbst darf nicht auf die Verfügbarkeit/Antwortzeit von `appointment-service` warten. Eventual Consistency ist hier fachlich akzeptabel (siehe Abschnitt 3, Kompensation). |
| `employee-service` → alle Konsumenten: `EmployeeDeactivated` | **Asynchron (Integration Event)** | Gleiche Begründung wie bei `CustomerDeactivated`: reine Statusänderung, kein blockierender Bedarf beim Sender. |
| `appointment-service` → Erinnerungs-Job/Reporting: `AppointmentCreated`, `AppointmentCancelled`, `AppointmentRescheduled` | **Asynchron (Integration Event)** | Diese Events haben keine synchronen Konsumenten innerhalb des Request-Zyklus; der Erinnerungs-Job liest sie zeitversetzt. Asynchrone Kopplung minimiert Kopplung/Latenz im Schreibpfad und ist der einzig sinnvolle Kanal für einen Hintergrunddienst. |

Damit ist die in den OpenSpecs offen gelassene Frage entschieden: **Validierungs-/Verfügbarkeits-Calls, die eine synchrone, request-blockierende Antwort für eine Schreibentscheidung benötigen, laufen als REST/Service Discovery; alle reinen Statusänderungs-Benachrichtigungen laufen als asynchrone Integration Events.** Ein Read-Cache in `appointment-service` für Kundendaten (wie in der Kundenverwaltungs-Spec als Alternative erwähnt) wird **nicht** eingeführt — die synchrone Existenz-/Aktivstatusprüfung ist einfacher, aktueller und ausreichend performant für den erwarteten Datenumfang eines CRM.

### 3. Datenkonsistenzstrategie

- **Innerhalb** eines Service: strikte Konsistenz (ACID via PostgreSQL-Transaktion pro Aggregate Root).
- **Zwischen** Services: Eventual Consistency für alle über Integration Events propagierten Statusänderungen (`CustomerDeactivated`, `EmployeeDeactivated`, `AppointmentCreated/Cancelled/Rescheduled`). Konsumenten müssen idempotent verarbeiten (Wolverine Inbox/Outbox-Pattern, Deduplizierung über Event-Id).
- **Race Condition "Kunde wird während Terminanlage deaktiviert":**
  1. `appointment-service` prüft synchron `GET /api/customers/{id}` unmittelbar vor dem Schreiben des Termins (read-your-writes so aktuell wie möglich).
  2. Trotzdem bleibt ein Zeitfenster zwischen Prüfung und Commit. Als Kompensation gilt: Wenn `customer-service` danach ein `CustomerDeactivated`-Event für einen Kunden mit zukünftigen `Confirmed`-Terminen veröffentlicht, konsumiert `appointment-service` dieses Event und markiert betroffene zukünftige Termine mit einem Folgestatus (`RequiresReview`) statt automatischer Stornierung; ein Mitarbeiter entscheidet manuell (kein automatischer Daten-Verlust, keine stille Inkonsistenz).
  3. Es wird **keine** verteilte Transaktion (2PC) verwendet. Bei Nichtverfügbarkeit von `customer-service` oder `employee-service` während der synchronen Vorprüfung wird die Terminanlage abgelehnt (Fail-Fast, siehe Resilienz) — kein inkonsistenter Zwischenzustand wird persistiert. Dies deckt sich mit der bereits in `terminverwaltung.openspec.md` Abschnitt 5 spezifizierten Anforderung.
  4. Dieses Muster ist eine leichtgewichtige Choreografie-Saga (kein zentraler Orchestrator): jeder Service reagiert eigenständig auf Events anderer Services und stellt lokale Konsistenz wieder her.

### 4. API-Gateway

Es wird ein **API-Gateway (Backend-for-Frontend/Reverse-Proxy)** vor den drei Services eingeführt (z.B. YARP, als eigenes Aspire-Ressourcenprojekt). Begründung:
- Das Angular-Frontend soll gegen eine einzige Basis-URL/Origin sprechen (CORS-Vereinfachung, einheitliche Authentifizierung/Token-Validierung an einer Stelle).
- Routing zu `customer-service`, `employee-service`, `appointment-service` erfolgt pfadbasiert (`/api/customers/**`, `/api/employees/**`, `/api/appointments/**`).
- Das Gateway übernimmt **keine** fachliche Aggregation/Orchestrierung (kein "Backend-for-Frontend" mit eigener Business-Logik) — es bleibt ein reiner Reverse-Proxy inkl. Auth-Termination (Microsoft Identity Token-Validierung), um die Bounded-Context-Grenzen nicht zu verwischen.
- Intern (Service-zu-Service) wird **nicht** über das Gateway geroutet, sondern direkt über .NET Aspire Service Discovery, um unnötige Latenz/SPOF zu vermeiden.

### 5. Resilienz-Anforderungen

Für alle synchronen Cross-Service-Calls (`appointment-service` → `customer-service`/`employee-service`, `employee-service` → `appointment-service`) gilt verbindlich, per Polly-Policy im jeweiligen HTTP-Client konfiguriert:
- **Timeout:** kurzer Timeout pro Call (Richtwert 2–3 Sekunden), da es sich um synchrone Vorbedingungsprüfungen im Request-Pfad handelt.
- **Retry:** maximal 2 Wiederholungen mit Exponential Backoff + Jitter, nur für transiente Fehler (5xx, Netzwerk-Timeouts), nicht für 4xx (fachliche Ablehnung wird nicht wiederholt).
- **Circuit-Breaker:** öffnet nach einer definierten Fehlerquote/-anzahl, um kaskadierende Lastspitzen auf einen bereits gestörten Service zu vermeiden.
- **Fallback:** kein Silent-Fallback auf angenommene Daten. Bei offenem Circuit-Breaker/Timeout wird der ursprüngliche Request (z.B. Terminanlage) mit einer klaren, für das Frontend verständlichen Fehlermeldung abgelehnt (kein inkonsistenter Zwischenzustand) — wie bereits in `terminverwaltung.openspec.md` Abschnitt 5 gefordert.
- Für die asynchrone Integration-Event-Verarbeitung gilt die Standard-Resilienz von Wolverine (Outbox/Inbox, automatische Retries mit Dead-Letter-Queue bei dauerhaftem Fehler).

### 6. Interne Clean Architecture je Service

Die Microservices-Zerlegung ist eine **zusätzliche äußere Schicht** und ersetzt nicht die in `CLAUDE.md` vorgeschriebene interne Clean Architecture. Jeder der drei Services (`customer-service`, `employee-service`, `appointment-service`) hält intern verbindlich folgende Schichtentrennung ein:
- **Domain:** Aggregate Roots (`Customer`, `Employee`, `Appointment`), Value Objects, Domain Events. Keine Abhängigkeit zu EF Core, HTTP-Clients oder Messaging-Infrastruktur. Insbesondere darf die Domain-Schicht von `appointment-service` **keinen** direkten Call zu anderen Services ausführen — Verfügbarkeits-/Kundendaten werden als bereits validierter Snapshot/Parameter aus der Application-Schicht in die Domain-Logik übergeben (wie bereits in `terminverwaltung.openspec.md` Abschnitt 7 festgelegt).
- **Application:** CQRS Commands/Queries via Wolverine, Repository-Interfaces, Orchestrierung der Cross-Service-Calls (HTTP-Clients, Event-Publishing) — die Orchestrierung selbst gehört in die Application-Schicht, nicht in die Domain-Schicht.
- **Infrastructure:** EF Core Repositories gegen die jeweils eigene Datenbank, HTTP-Client-Implementierungen (mit Polly), Messaging-Publisher/-Consumer (Wolverine/RabbitMQ).
- **Presentation/Web:** Minimal-API/Controller-Endpunkte, kommunizieren ausschließlich mit der Application-Schicht.

Diese interne Struktur gilt unverändert für alle drei Services und ist bei jedem High-Level-Review vor "Done"-Markierung zu prüfen.

### 7. Deployment/Orchestrierung über .NET Aspire

Ein zentrales `AppHost`-Projekt orchestriert:
- `customer-service`, `employee-service`, `appointment-service` (jeweils eigenes Web-Projekt),
- je eine PostgreSQL-Instanz/-Container pro Service (`customer_db`, `employee_db`, `appointment_db`) — kein gemeinsamer Datenbank-Server mit geteiltem Schema,
- den Message Broker (RabbitMQ-Container) für Wolverine Messaging,
- das API-Gateway (YARP) als zusätzliche Ressource,
- Service-Discovery/Referenzen zwischen den Projekten ausschließlich über `AppHost`-Ressourcenverweise (`WithReference`), keine hartkodierten URLs (siehe `CLAUDE.md`, Abschnitt Infrastruktur).

Docker bleibt die Grundlage für alle containerisierten Abhängigkeiten (PostgreSQL, RabbitMQ), lokal über Aspire AppHost gestartet.

## Consequences

**Positiv:**
- Klare, für alle drei Teams (Backend-Agent-Instanzen) verbindliche Regel, wann synchron vs. asynchron kommuniziert wird — verhindert Ad-hoc-Entscheidungen pro Feature.
- Choreografie-Ansatz statt zentralem Saga-Orchestrator hält die Services lose gekoppelt und vermeidet einen weiteren Single Point of Failure.
- API-Gateway vereinfacht Frontend-Integration erheblich, ohne die Bounded-Context-Grenzen serverseitig aufzuweichen.
- Resilienz-Vorgaben (Polly) verhindern, dass ein Ausfall von `customer-service`/`employee-service` den `appointment-service` unkontrolliert blockiert.

**Negativ / Trade-offs:**
- Synchrone Calls erzeugen Laufzeit-Kopplung zwischen `appointment-service` und den anderen beiden Services; Verfügbarkeit von `appointment-service` hängt von deren Erreichbarkeit ab (bewusst akzeptiert, siehe Resilienz-Policy).
- Eventual Consistency bei Deaktivierungs-Events erfordert zusätzlichen Status `RequiresReview` und manuelle Nacharbeit statt vollautomatischer Konsistenz — höhere Komplexität in `appointment-service`, aber notwendig, um Datenverlust/stille Fehlzustände zu vermeiden.
- Zusätzliche Infrastrukturkomponenten (API-Gateway, Message Broker) erhöhen den Betriebs- und Orchestrierungsaufwand gegenüber einem modularen Monolithen.
- Idempotenz-Handling (Outbox/Inbox) muss in jedem Event-Konsumenten sauber implementiert werden — Mehraufwand, der bei fehlender Disziplin zu Doppelverarbeitung führen kann.

**Folgeaufgaben:**
- `backend-agent`: HTTP-Client-Verträge (typisierte Clients) für `GET /api/employees/{id}/availability` und `GET /api/customers/{id}` inkl. Polly-Policies gemäß Abschnitt 5 implementieren.
- `backend-agent`: Wolverine-Event-Contracts für `CustomerDeactivated`, `EmployeeDeactivated`, `AppointmentCreated`, `AppointmentCancelled`, `AppointmentRescheduled` versionieren (Schema-Kompatibilität).
- `devops-agent`: AppHost-Projekt mit allen drei Services, drei PostgreSQL-Ressourcen, RabbitMQ und API-Gateway aufsetzen.
- `sec-agent`: Auth-Termination/Token-Validierung im API-Gateway sowie Service-zu-Service-Absicherung (z.B. Service-to-Service-Token) abstimmen.
