# 0002. Frontend-Konsolidierung: eine gemeinsame Angular-Anwendung statt Client-pro-Service

> **Status:** accepted
> **Datum:** 2026-09-30
> **Autor:** architect-agent
> **Betrifft:** `UserStories/frontend-konsolidierung.openspec.md`, `UserStories/kundenverwaltung.openspec.md` (Abschnitt 4), `UserStories/mitarbeiterverwaltung.openspec.md` (Abschnitt 4), `UserStories/terminverwaltung.openspec.md` (Abschnitt 4)
> **Ersetzt/präzisiert:** `docs/adr/0001-microservices-architektur-crm.md`, ausschließlich hinsichtlich der dort implizit getroffenen Frontend-Aufteilung (ein Angular-Client pro Backend-Service: `customer-web`, `employee-web`, `appointment-web`). Alle übrigen Entscheidungen aus ADR 0001 (Servicegrenzen, Kommunikationsmuster, Konsistenzstrategie, Gateway, Resilienz, interne Clean Architecture, Aspire-Orchestrierung) bleiben unverändert und werden durch diese ADR nicht berührt.

## Context

ADR 0001 legt fest, dass das CRM als drei fachliche Bounded Contexts (`customer-service`, `employee-service`, `appointment-service`) mit eigener Datenbank pro Service umgesetzt wird, und führt in Abschnitt 4 ein API-Gateway ein, "damit das Angular-Frontend gegen eine einzige Basis-URL/Origin spricht". In der praktischen Umsetzung sind daraus jedoch drei separate Angular-Anwendungen entstanden (`customer-web`, `employee-web`, `appointment-web`), jede mit eigenem Login-Flow, eigenem Dev-Port (4200/4201/4202) und eigenem Deployment. Das ist eine gängige, aber nicht in ADR 0001 zwingend vorgeschriebene Interpretation von "ein Client pro Service".

In der Praxis erzeugt das:
- dreifachen, identischen Auth-Boilerplate (`AuthStateService`, `authInterceptor`, `authGuard`, Login-Screen) in allen drei Apps,
- drei Gateway-CORS-Origins statt einem (vom `qa-agent` bereits als Risikofläche identifiziert),
- einen für Endnutzer fragmentierten Arbeitsablauf: ein Wechsel zwischen Kunde/Mitarbeiter/Termin erfordert einen App-/Tab-/Port-Wechsel, obwohl es sich fachlich um ein zusammenhängendes CRM handelt.

Der Product Owner hat dies in `UserStories/frontend-konsolidierung.openspec.md` als Change-Spec aufgenommen und fordert eine einzige Angular-Anwendung. Da dies von der bisherigen, in ADR 0001 etablierten Praxis abweicht, ist eine explizite Architekturentscheidung erforderlich.

**Warum eine neue ADR statt Änderung von ADR 0001:** ADRs werden nicht nachträglich umgeschrieben, sondern historisch fortgeschrieben. ADR 0001 bleibt als Dokumentation des ursprünglichen Kontexts und der zum damaligen Zeitpunkt gültigen Entscheidung bestehen; diese ADR 0002 dokumentiert die bewusste, punktuelle Korrektur an genau einer Stelle (Frontend-Topologie) und verweist auf ADR 0001 für alles, was unverändert gilt.

## Decision

### Was unverändert bleibt (ADR 0001 gilt weiterhin vollständig)

- Servicegrenzen und Bounded Contexts (`customer-service`, `employee-service`, `appointment-service`), jeweils mit eigener Datenbank ("Database per Service").
- Kommunikationsmuster zwischen den Backend-Services (synchrone REST-Calls für Verfügbarkeits-/Kundenvalidierung, asynchrone Integration Events für Statusänderungen), Konsistenzstrategie, Resilienz-Policies (Polly), interne Clean Architecture je Service, Aspire-Orchestrierung.
- Das API-Gateway (YARP) als einziger Zugriffspfad für das Frontend auf alle Backend-Services; Service-zu-Service-Kommunikation bleibt weiterhin direkt über Aspire Service Discovery, nicht über das Gateway.

Diese Entscheidung betrifft **ausschließlich die Presentation-Schicht** (Anzahl und Struktur der Angular-Anwendungen). Sie ist keine Neubewertung der Microservices-Architektur und erfordert keinen Rückbau der Backend-Services.

### Was sich ändert

Statt drei eigenständiger Angular-Anwendungen (ein Client pro Backend-Service) wird **eine einzige Angular-22-Standalone-Anwendung `Clients/crm-web`** eingeführt:
- Eine gemeinsame Shell (Header, Navigation, Router-Outlet) mit genau einem Login-Flow, einem `AuthStateService`, einem `authInterceptor`, einem `authGuard` für die gesamte Anwendung.
- Drei lazy-geladene Feature-Bereiche `/customers/**`, `/employees/**`, `/appointments/**`, die die bestehenden Komponenten aus `customer-web`, `employee-web`, `appointment-web` unverändert übernehmen (reine strukturelle Migration, keine fachliche Neuentwicklung, keine Verhaltensänderung der bestehenden Scenarios).
- Ein gemeinsamer i18n-Satz (`de.json`/`en.json`) statt drei getrennter i18n-Dateien.
- Weiterhin ausschließlich Zugriff auf die Backend-Services über das YARP-Gateway (`/api/auth/**`, `/api/customers/**`, `/api/employees/**`, `/api/appointments/**`); an den Gateway-Routen selbst ändert sich nichts.

### Begründung / Trade-off

Der theoretische Entkopplungsvorteil von drei unabhängig deploybaren Frontends wird für ein CRM dieser Größe durch den Mehraufwand nicht gerechtfertigt: dreifacher, identischer Auth-/Interceptor-/Guard-Code, drei CORS-Origins, fragmentierte UX bei einem fachlich zusammenhängenden Arbeitsablauf (ein Sachbearbeiter arbeitet typischerweise in einer Sitzung mit Kunden, Mitarbeitern und Terminen). Eine gemeinsame Shell mit lazy-geladenen Feature-Modulen bietet dieselbe Modularität auf Code-Ebene (Feature-Bereiche bleiben in sich geschlossen, `customers`/`employees`/`appointments` je mit eigener `domain|infrastructure|application|presentation`-Struktur), ohne die Nachteile getrennter Deployments/Logins zu erzeugen. Dies ist eine bewusste, pragmatische Presentation-Layer-Entscheidung, kein Widerspruch zur Backend-Microservices-Entscheidung: "ein Client pro Service" ist keine zwingende Konsequenz von Microservices, sondern eine von mehreren möglichen Frontend-Topologien (analog zu "Micro-Frontends" vs. "monolithisches Frontend über mehreren Backend-Services").

### Konsequenzen für die Migration

- **Gateway-CORS**: `Cors:AllowedOrigins` in `Gateway/Crm.Gateway/appsettings.json` (und Fallback in `Program.cs`) wird von drei Frontend-Origins (4200/4201/4202) auf den einen `crm-web`-Dev-Origin reduziert.
- **Alte Projekte entfernen**: Nach erfolgreicher, grüner Migration (alle bestehenden Scenarios weiterhin abgedeckt, plus neue Shell-/Navigations-Tests) werden `Clients/customer-web`, `Clients/employee-web`, `Clients/appointment-web` sowie die zugehörigen Testordner entfernt und durch `Tests/CrmWeb.Tests` ersetzt.
- **E2E-Tests**: `Tests/E2E/` wird auf eine einzige Basis-URL statt drei umgestellt.
- **Dokumentation**: `AppHost/README.md` und weitere Doku mit Verweis auf die drei alten Dev-Ports werden aktualisiert.
- Details und Task-Checkliste siehe `UserStories/frontend-konsolidierung.openspec.md`.

## Consequences

**Positiv:**
- Einheitliche, konsistente UX; kein App-Wechsel für zusammengehörige fachliche Arbeitsabläufe.
- Nur noch ein Auth-Flow/Interceptor/Guard zu pflegen statt drei identischer Implementierungen.
- Ein Gateway-CORS-Origin statt drei — reduziert Angriffsfläche und Konfigurationsaufwand (vom `qa-agent` als Risiko identifiziert).
- Ein Frontend-Deployment statt drei.

**Negativ / Trade-offs:**
- Verlust der unabhängigen Deploybarkeit der drei Frontend-Teile (bewusst akzeptiert, da für dieses Projekt kein realer Nutzen daraus gezogen wurde).
- Ein einzelner Build-/Deployment-Fehler in `crm-web` kann potenziell alle drei fachlichen Bereiche gleichzeitig betreffen, statt nur einen (Risiko wird durch lazy-loading und getrennte Feature-Module auf Code-Ebene begrenzt, aber nicht vollständig eliminiert).
- Einmaliger Migrationsaufwand (Komponenten, Tests, i18n, CORS, Doku).

**Folgeaufgaben:**
- `frontend-agent`: `Clients/crm-web` gemäß `UserStories/frontend-konsolidierung.openspec.md` Abschnitt 7/8 aufsetzen und die drei bestehenden Apps migrieren.
- `qa-agent`: `Tests/E2E/` auf eine Basis-URL umstellen, alle bestehenden E2E-Tests gegen `crm-web` grün halten.
- `devops-agent`: Gateway-CORS-Konfiguration sowie AppHost-Referenzen auf `crm-web` anpassen, alte App-Ressourcen entfernen.
- `architect-agent`: Vor dem Archivieren der Change-Spec bestätigen, dass Umsetzung (Struktur, Auth-Zusammenführung, Gateway-Zugriff ausschließlich über bestehende Routen) mit dieser ADR übereinstimmt.
