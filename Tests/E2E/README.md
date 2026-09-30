# CRM E2E-Tests (Playwright)

End-to-End-Tests fuer die drei Kern-Capabilities des CRM (Kundenverwaltung, Mitarbeiterverwaltung,
Terminverwaltung inkl. RBAC) sowie den Authentifizierungs-Flow, abgeleitet direkt aus den
Scenarios (GIVEN/WHEN/THEN) in:

- `UserStories/kundenverwaltung.openspec.md`
- `UserStories/mitarbeiterverwaltung.openspec.md`
- `UserStories/terminverwaltung.openspec.md`
- `UserStories/authentifizierung.openspec.md`

## Warum Playwright (statt Cypress)

- Ein Testlauf muss echten Cross-Origin-Login gegen `identity-service` unterstuetzen. Playwright
  hat dafuer kein Origin-Lock-in (anders als Cypress' `cy.origin()`-Workaround) und startet pro
  Test einen frischen Browser-Context. (Historisch lief die Suite gegen drei unabhaengige
  Angular-Origins (customer-web:4200, employee-web:4201, appointment-web:4202); seit der
  Frontend-Konsolidierung, siehe `docs/adr/0002-frontend-konsolidierung.md`, gibt es nur noch
  einen Origin: `crm-web:4200`.)
- Native `request`-/Fetch-Unterstuetzung fuer das "API-first, UI-second"-Testdatenmuster
  (siehe `fixtures/api.ts`): Testdaten (Kunde, Mitarbeiter, Termin) schnell per API anlegen,
  die eigentliche Handlung (Formular, Klick, Fehlermeldung) ausschliesslich per echter
  Browser-Interaktion pruefen.
- Eingebauter Trace-Viewer/Video/Screenshot-on-failure ohne Zusatzpakete - hilfreich bei einer
  Multi-Service-Landschaft, in der Fehlerursachen (Frontend vs. Backend vs. Netzwerk) schnell
  eingegrenzt werden muessen.

## Voraussetzungen

- Node.js 20+ (getestet mit Node 26).
- Die vier Backend-Services (`identity-service`, `customer-service`, `employee-service`,
  `appointment-service`) UND der eine konsolidierte Angular-Dev-Server (`crm-web`, Port 4200)
  laufen bereits. Diese Suite startet selbst **keine** Server (kein Playwright-`webServer`-Block),
  da in dieser Microservices-Landschaft zu viele unabhaengige Backend-Prozesse beteiligt sind.

### Seed-Testnutzer (identity-service)

Aus `Services/identity-service/src/IdentityService.Infrastructure/Users/IdentitySeedOptions.cs`
und `IdentityService.Web/appsettings.json`:

| Rolle    | E-Mail                 | Passwort |
|----------|-------------------------|----------|
| Admin    | `admin@crm.local`      | **nicht fest im Code hinterlegt** - siehe unten |
| Employee | `employee@crm.local`   | **nicht fest im Code hinterlegt** - siehe unten |

`IdentitySeedOptions.AdminPassword`/`EmployeePassword` sind standardmaessig leer. Ist kein
Passwort konfiguriert, generiert `InMemorySeedUserRepository` beim Start (nur in
`Development`) ein zufaelliges Passwort und loggt es **einmalig** in der Konsole des
identity-service-Prozesses (kein Hardcoding, kein Produktionsrisiko - siehe Kommentare in
`IdentitySeedOptions.cs`). Damit die E2E-Suite mit **bekannten** Zugangsdaten laufen kann,
vor dem Start entweder:

- **Via Aspire AppHost** (empfohlen, siehe `AppHost/README.md`):
  ```bash
  cd AppHost/Crm.AppHost
  dotnet user-secrets set "Parameters:jwt-signing-key" "$(openssl rand -base64 32)"
  dotnet user-secrets set "Parameters:identity-admin-password" "Admin123!"
  dotnet user-secrets set "Parameters:identity-employee-password" "Employee123!"
  dotnet run
  ```
  **Hinweis (siehe Bug 3 unten, mittlerweile behoben):** Fruehere Sessions in diesem Dokument
  beschrieben einen 307-Redirect-Konflikt zwischen Aspire-HTTP/HTTPS-Doppelbindung und
  `UseHttpsRedirection()` in den internen Services, der den AppHost-Start fuer diese Suite
  unbrauchbar machte. Der Fix (devops-agent, 2026-09-30) hat `UseHttpsRedirection()` aus den
  vier internen Services entfernt; der AppHost-Start ist seitdem real gegen `GET /api/customers`
  ueber das Gateway verifiziert (kein 307 mehr). Diese Suite wurde in der urspruenglichen Session
  trotzdem gegen einen **manuellen Einzelstart** verifiziert (naechster Punkt) - das bleibt als
  historische, weiterhin gueltige Alternative erhalten.

- **Manueller Einzelstart** (so wurde in dieser Session tatsaechlich verifiziert): jeden
  Service einzeln mit seinem `http`-Launch-Profile starten (feste Ports aus
  `launchSettings.json`, identisch zu den Angular-`environment.ts`-Defaults), z.B.:
  ```bash
  # Postgres/RabbitMQ vorher per Docker bereitstellen (z.B. kurzzeitig ueber den AppHost
  # hochfahren und die Container-Verbindungsdaten uebernehmen, oder eigene Container starten).
  cd Services/identity-service/src/IdentityService.Web
  ASPNETCORE_ENVIRONMENT=Development \
  Authentication__JwtBearer__SigningKey="<32-Byte-Secret>" \
  IdentitySeed__AdminPassword="Admin123!" \
  IdentitySeed__EmployeePassword="Employee123!" \
  dotnet run --launch-profile http

  cd Services/customer-service/src/CustomerService.Web
  ASPNETCORE_ENVIRONMENT=Development \
  Authentication__JwtBearer__SigningKey="<gleiches Secret>" \
  ConnectionStrings__customer_db="Host=127.0.0.1;Port=<pg-port>;Username=postgres;Password=<pw>;Database=customer_db" \
  ConnectionStrings__rabbitmq="amqp://guest:<pw>@127.0.0.1:<port>/" \
  dotnet run --launch-profile http

  # analog fuer employee-service (Port 5159, ConnectionStrings__employee_db,
  # zusaetzlich APPOINTMENT_SERVICE_BASE_URL=http://localhost:5169) und
  # appointment-service (Port 5169, ConnectionStrings__appointment_db,
  # CUSTOMER_SERVICE_BASE_URL=http://localhost:5149, EMPLOYEE_SERVICE_BASE_URL=http://localhost:5159)
  ```
  Danach das konsolidierte Angular-Frontend:
  ```bash
  cd Clients/crm-web && npx ng serve --port 4200
  ```

Alle Basis-URLs (Web + API) sind zusaetzlich per Umgebungsvariable ueberschreibbar
(siehe `fixtures/env.ts`), falls Ports abweichen (z.B. `E2E_CRM_WEB_URL`,
`E2E_CUSTOMER_API_URL`, `E2E_ADMIN_PASSWORD`, ...). `urls.customerWeb`/`urls.employeeWeb`/
`urls.appointmentWeb` sind Aliase auf dieselbe `crm-web`-Basis-URL, damit die bestehenden
Spec-Dateien unveraendert funktionieren.

## Setup & Ausfuehrung

```bash
cd Tests/E2E
npm install
npx playwright install chromium   # einmalig
npm run list                      # nur strukturelle Pruefung, kein echter Lauf
npm test                          # echter Lauf gegen die laufenden Anwendungen
npm run report                    # HTML-Report der letzten Ausfuehrung oeffnen
```

## Struktur

```
Tests/E2E/
  fixtures/
    env.ts     Basis-URLs & Seed-Testnutzer (per Env-Var ueberschreibbar)
    api.ts     API-Test-Helfer fuer schnelle, deterministische Testdaten-Vorbereitung
    auth.ts    UI-Login-Helfer (echtes Formular, kein Session-Bypass)
  tests/
    auth.spec.ts          Login/Session-Handling (authentifizierung.openspec.md)
    customers.spec.ts     Kundenverwaltung (kundenverwaltung.openspec.md)
    employees.spec.ts     Mitarbeiterverwaltung (mitarbeiterverwaltung.openspec.md)
    appointments.spec.ts  Terminverwaltung ohne RBAC (terminverwaltung.openspec.md)
    rbac.spec.ts          RBAC-Requirement "Sichtbarkeit von Terminen nach Rolle"
```

## Abdeckung der Scenarios (Abschnitt 3 der jeweiligen OpenSpec)

Jedes SHALL-Requirement aus den vier Change-Specs hat mindestens einen Test:

- **authentifizierung.openspec.md**: Login Admin/Employee (Erfolg), falsches Passwort,
  unbekannte E-Mail, Navigation ohne Session -> Login-Redirect, automatischer Logout bei 401.
  (Rate-Limiting/reine Ownership-Backend-Scenarios bleiben bei den Backend-Testsuiten
  `Tests/IdentityService.Tests`/`Tests/EmployeeService.Tests`, wie in der Spec selbst
  vorgesehen - "reine Backend-Scenarios ... sind Gegenstand der Backend-Testsuiten".)
- **kundenverwaltung.openspec.md**: Anlegen (gueltig/ungueltig), Suche, Bearbeiten,
  Deaktivieren, Detailsicht/Terminhistorie-Tab.
- **mitarbeiterverwaltung.openspec.md**: Anlegen (gueltig/E-Mail-Konflikt), Leistung
  zuordnen, Arbeitszeit hinterlegen, Abwesenheit (frei/Konflikt mit Bestaetigung),
  Deaktivieren.
- **terminverwaltung.openspec.md**: Anlegen im Zeitfenster, ausserhalb der Arbeitszeit,
  Doppelbuchung, Verschieben, Stornieren, Wochenansicht, RBAC (Admin sieht alle, Employee
  sieht keine fremden, 403 bei Fremdzugriff auf Verschieben/Stornieren mit UI-Fehlermeldung).

## Tatsaechliche Verifikation in dieser Session - ehrlicher Stand

**Strukturell verifiziert:** Alle 30 Tests wurden erfolgreich geparst/gelistet
(`npx playwright test --list`, siehe Ausgabe im QA-Abschlussbericht) - keine Syntaxfehler,
korrekte Testverteilung auf 4 Projekte/Origins.

**Echt gegen ein laufendes System verifiziert (nicht nur Unit-/Mock-Ebene):**
- Das Gesamtsystem (identity-service, customer-service, employee-service, appointment-service,
  Postgres x3, RabbitMQ, alle drei Angular-Dev-Server) wurde tatsaechlich lokal hochgefahren
  (Docker war in dieser Umgebung verfuegbar).
- `tests/auth.spec.ts`: **5 von 6 Tests liefen tatsaechlich gruen** gegen den echten
  identity-service + customer-web (echter Login als Admin/Employee, falsches Passwort,
  unbekannte E-Mail, Navigation-ohne-Session-Redirect). Der 6. Test (automatischer Logout
  bei 401) konnte in dieser Session **nicht** verifiziert werden - nicht wegen eines Fehlers
  im Test, sondern weil er einen echten Request an `customer-service` braucht, was durch
  Bug #1 unten blockiert ist.
- `tests/customers.spec.ts`, `tests/employees.spec.ts`, `tests/appointments.spec.ts`,
  `tests/rbac.spec.ts`: **in dieser Session nicht end-to-end gruen verifizierbar**, weil
  `customer-service`/`employee-service`/`appointment-service` aktuell **keine CORS-Middleware**
  konfiguriert haben (siehe Bug #1) - jeder Request des Browsers an diese drei Services wird
  vom Browser selbst blockiert, unabhaengig davon, ob der Test korrekt geschrieben ist. Ein
  lokaler Verifikations-Patch (identische `AddCors`/`UseCors`-Konfiguration wie in
  `identity-service`/Gateway, temporaer, nur zur Bestaetigung der Diagnose) wurde in dieser
  Session probeweise eingespielt, bestaetigte die Diagnose (Requests gingen danach durch) und
  wurde **anschliessend vollstaendig ueber `git checkout` zurueckgesetzt** - es ist keine
  Aenderung an Produktivcode im finalen Diff enthalten; das Beheben ist Aufgabe des
  `backend-agent`, nicht des QA-Agent.
- **Fazit:** Die Tests sind korrekt geschrieben und wuerden nach Behebung von Bug #1 (und mit
  den in `rbac.spec.ts` dokumentierten Einschraenkungen zu Bug #2) laufen. Ein finaler
  "alle 30 gruen"-Lauf steht noch aus und sollte vom backend-agent nach dem CORS-Fix angefordert
  werden.

## Gefundene Bugs (an die zustaendigen Dev-Agenten zurueckgegeben, nicht selbst gefixt)

### Bug 1 (P0, backend-agent): Fehlende CORS-Konfiguration in customer-service/employee-service/appointment-service — BEHOBEN

**Status:** Behoben (backend-agent, 2026-09-30). `builder.Services.AddCors(...)` (Policy
`"frontend"`, Quelle `Cors:AllowedOrigins`, kein Wildcard) + `app.UseCors("frontend")` (vor
`UseAuthentication`) wurden analog zu `identity-service`/Gateway in allen drei `Program.cs`
ergaenzt (`Services/customer-service/src/CustomerService.Web/Program.cs`,
`Services/employee-service/src/EmployeeService.Web/Program.cs`,
`Services/appointment-service/src/AppointmentService.Web/Program.cs`), inklusive
`"Cors": { "AllowedOrigins": ["http://localhost:4200"] }` in den jeweiligen `appsettings.json`
(einziger gueltiger Origin nach der Frontend-Konsolidierung, ADR 0002). Real gegen den laufenden
AppHost verifiziert: `OPTIONS`-Preflight mit `Origin: http://localhost:4200` liefert jetzt
`204` mit `Access-Control-Allow-Origin: http://localhost:4200` auf allen drei Services; ein
nicht erlaubter Origin (`http://evil.example`) erhaelt weiterhin keine CORS-Header. `dotnet build`/
`dotnet test` (124 Tests) bleiben gruen. Die urspruengliche Bug-Beschreibung unten bleibt zur
Nachvollziehbarkeit unveraendert erhalten.

### Bug 1 (P0, backend-agent): Fehlende CORS-Konfiguration in customer-service/employee-service/appointment-service

`customer-service`, `employee-service` und `appointment-service` haben **keine**
`AddCors`/`UseCors`-Konfiguration (weder in `Program.cs` noch `appsettings.json` -
im Gegensatz zu `identity-service` und dem Gateway, die beide korrekt eine `Cors`-Policy
haben). Jeder echte Cross-Origin-Request eines Browsers (z.B. `customer-web` auf
`localhost:4200` gegen `customer-service` auf `localhost:5149`) mit einem
`Authorization`-Header (das bei praktisch jedem authentifizierten Call der Fall ist) loest
einen CORS-Preflight (`OPTIONS`) aus, der ohne `Access-Control-Allow-Origin`-Header vom
Browser abgelehnt wird - **bevor** die eigentliche Anfrage den Server ueberhaupt erreicht.
Nachweis (Browser-Konsole, reproduziert in dieser Session):
```
Access to fetch at 'http://localhost:5149/api/customers?page=1&status=true' from origin
'http://localhost:4200' has been blocked by CORS policy: Response to preflight request
doesn't pass access control check: No 'Access-Control-Allow-Origin' header is present.
```
**Auswirkung:** In der aktuellen Dev-Konfiguration (Frontends sprechen direkt gegen die
Services, siehe `environment.ts`) sind `customer-web`, `employee-web` und `appointment-web`
fuer praktisch **jede** authentifizierte Funktion (Kundenliste, Mitarbeiterliste, Kalender,
alle Formulare) in einem echten Browser nicht funktionsfaehig, obwohl Unit-/Integrationstests
(die keine Browser-CORS-Regeln kennen) und einfache `curl`-Aufrufe unauffaellig bleiben - der
Bug ist ausschliesslich im echten Browser sichtbar, was erklaert, warum er bislang nicht
aufgefallen ist.
**Fix-Vorschlag:** analog zu `identity-service`/Gateway `builder.Services.AddCors(...)` +
`app.UseCors(...)` (vor `UseAuthentication`) in allen drei `Program.cs` ergaenzen, plus
`"Cors": { "AllowedOrigins": [...] }` in den jeweiligen `appsettings.json`.

### Bug 2 (P1, backend-agent/sec-agent): Keine Verknuepfung zwischen identity-service-User und employee-service-Employee — VORAUSSETZUNG BEHOBEN

**Status:** Die fehlende Id-Verknuepfung ist behoben (backend-agent, 2026-09-30). Der geseedete
identity-service-Testnutzer `employee@crm.local` hatte bereits eine DETERMINISTISCHE
User-Id (`InMemorySeedUserRepository.DeterministicIdFor`, MD5-Hash der lowercased E-Mail,
nicht zufaellig - keine Aenderung noetig). Neu: `employee-service` besitzt jetzt einen
dev-/test-only Seeder (`EmployeeService.Infrastructure.Seed.DevEmployeeSeedHostedService`,
NUR in `Development` aktiv), der beim Start genau einen `Employee`-Datensatz mit exakt
dieser bekannten Id (`e573eed0-8c40-749f-f05c-aa6839c81cb4`, Default in
`DevEmployeeSeedOptions.EmployeeUserId`) anlegt, falls noch keiner existiert. Domain-Layer:
neue Factory `Employee.CreateWithId(Guid id, ...)` (nur fuer diesen Seeder vorgesehen, regulaere
API-Erstellung nutzt weiterhin `Employee.Create` mit serverseitig generierter Id). Real gegen
den laufenden AppHost verifiziert: `POST /api/auth/login` mit `employee@crm.local` liefert
`userId: e573eed0-8c40-749f-f05c-aa6839c81cb4`; in `employee_db` existiert nach Service-Start
ein `Employee`-Datensatz mit exakt dieser Id (per `psql` gegen den laufenden Postgres-Container
geprueft). Damit ist die VORAUSSETZUNG fuer den positiven Ownership-Pfad jetzt erfuellt - das
`test.fixme(...)` in `rbac.spec.ts` bleibt bewusst unveraendert (wird vom qa-agent bei dessen
Verifikationslauf entfernt), da bei der Live-Verifikation zusaetzlich **Bug 4** (siehe unten)
aufgefallen ist, der `GET /api/employees/{id}` und `GET /api/employees` aktuell unabhaengig
davon mit `500` fehlschlagen laesst - der qa-agent kann `rbac.spec.ts` daher erst nach Behebung
von Bug 4 vollstaendig durchlaufen lassen. `dotnet build`/`dotnet test` (128 Tests ueber
EmployeeService.Tests/IdentityService.Tests/CustomerService.Tests/AppointmentService.Tests)
bleiben gruen. Die urspruengliche Bug-Beschreibung unten bleibt zur
Nachvollziehbarkeit unveraendert erhalten.

Der RBAC-Ownership-Check in `appointment-service`/`employee-service` vergleicht den
`NameIdentifier`-Claim des JWT direkt mit `Appointment.EmployeeId`/`Employee.Id`. Der
geseedete `identity-service`-Testnutzer `employee@crm.local` hat eine **deterministische,
aus der E-Mail abgeleitete** User-Id (`InMemorySeedUserRepository.DeterministicIdFor`).
`employee-service` vergibt beim Anlegen eines Mitarbeiters (`Employee.Create`) aber immer
eine **serverseitig neu generierte** `Guid.NewGuid()` - es gibt keinen Mechanismus, der
diese beiden Ids verknuepft. Ergebnis: der reale Login als `employee@crm.local` kann
**niemals** einem echten `Employee`-Datensatz "gehoeren" und sieht daher nie eigene Termine
oder darf nie eine eigene Abwesenheit anlegen - das positive Ownership-Scenario
("Mitarbeiter legt eigene Abwesenheit an" / "Employee sieht eigene Termine") ist mit den
aktuellen Seed-Daten **nicht** ueber einen echten Login-Flow erzeugbar. In `rbac.spec.ts`
ist der betroffene Fall als `test.fixme(...)` markiert; in `employees.spec.ts` wird die
Abwesenheits-Erfolgs-Scenario stattdessen ueber den Admin-Pfad getestet (Admin darf laut
Spec ohnehin fuer beliebige Mitarbeiter Abwesenheiten anlegen).
**Fix-Vorschlag:** Employee-Datensatz beim Anlegen optional mit einer vom Aufrufer
vorgegebenen Id (z.B. der User-Id aus identity-service) anlegen lassen, oder ein
"Employee-Self-Service-Onboarding" einfuehren, das beim ersten Login eines Employee-Nutzers
automatisch einen Employee-Datensatz mit passender Id anlegt/verknuepft.

### Bug 3 (P1, devops-agent): AppHost bindet Services gleichzeitig auf HTTP und HTTPS -> UseHttpsRedirection bricht Gateway-Pfad — BEHOBEN

**Status:** Behoben (devops-agent, 2026-09-30). `app.UseHttpsRedirection()` wurde aus den
vier internen Services entfernt (`Services/identity-service/src/IdentityService.Web/Program.cs`,
`Services/customer-service/src/CustomerService.Web/Program.cs`,
`Services/employee-service/src/EmployeeService.Web/Program.cs`,
`Services/appointment-service/src/AppointmentService.Web/Program.cs`), da diese laut ADR 0001
ohnehin nicht direkt von aussen erreichbar sein sollen - TLS-Termination bleibt ausschliesslich
beim Gateway (`Gateway/Crm.Gateway/Program.cs`, `UseHttpsRedirection()` dort unveraendert). Kein
Eingriff in `AppHost/Crm.AppHost/AppHost.cs` noetig (geringste Abweichung von der bestehenden
Struktur); ein optionaler naechster Schritt waere, den internen Services im AppHost gar keinen
HTTPS-Endpoint mehr zuzuweisen, ist aber fuer diesen Fix nicht erforderlich, da ohne die
Redirect-Middleware kein 307 mehr entsteht, selbst wenn der ungenutzte HTTPS-Port weiterhin
existiert. Real gegen den laufenden AppHost verifiziert: `GET /api/customers` ueber
`https://localhost:<gateway-port>` liefert ohne Token jetzt `401` (statt vorher `307` mit
`Location` auf den Service-Direktport) und mit gueltigem Admin-Token eine echte Antwort vom
Gateway-Pfad (kein Redirect mehr, siehe Response-Header ohne `Location`). `dotnet build`/
`dotnet test` (124 Tests) bleiben gruen. Die urspruengliche Bug-Beschreibung unten bleibt zur
Nachvollziehbarkeit unveraendert erhalten.

Wird das System ueber `AppHost/Crm.AppHost` gestartet, weist Aspire jedem Service sowohl
einen HTTP- als auch einen HTTPS-Endpunkt zu (`ASPNETCORE_URLS=https://...;http://...`).
Jeder Service ruft aber unbedingt `app.UseHttpsRedirection()` auf. Das YARP-Gateway spricht
die Downstream-Services intern per **HTTP** an (`"Address": "http://identity-service"` etc.
in `Gateway/Crm.Gateway/appsettings.json`); da der jeweilige Service aber *auch* einen
HTTPS-Port kennt, antwortet er auf den internen HTTP-Request mit `307 Redirect` auf seine
**eigene, direkte** HTTPS-Adresse (z.B. `https://localhost:50319/api/customers`) - fuer einen
echten Browser-Client hinter dem Gateway ist das nicht erreichbar/sinnvoll und widerspricht
dem in `docs/adr/0001-microservices-architektur-crm.md` festgelegten "Gateway ist einziger
Eintrittspunkt". Reproduziert in dieser Session per `curl` gegen den laufenden AppHost
(`/api/auth/login` und `/api/customers` liefern beide `307` mit `Location` auf den jeweiligen
Service-Direktport statt eine Antwort ueber das Gateway durchzureichen).
**Fix-Vorschlag:** entweder `UseHttpsRedirection()` in den Services entfernen (TLS-Termination
liegt laut ADR 0001 sowieso beim Gateway) oder `ForwardedHeaders`-Middleware mit korrektem
`X-Forwarded-Proto` einsetzen, damit die Redirect-Middleware den vom Gateway bereits
TLS-terminierten Request nicht faelschlich als "unsicher" einstuft.

### Bug 4 (P0, backend-agent): Wolverine-Codegen scheitert fuer Handler mit scoped EF-Core-Repository gegen echte Datenbank

**Neu entdeckt** (backend-agent, 2026-09-30) bei der Live-Verifikation von Bug 2: gegen eine
ECHTE `employee_db` (Migration angewendet, kein Fake-Repository) liefern `GET /api/employees`
und `GET /api/employees/{id}` beide `500 Internal Server Error`. Server-Log (siehe
`GlobalExceptionHandler`):

```
Wolverine.Configuration.InvalidServiceLocationException: Found service locations while
generating code for Message Handler for EmployeeService.Application.Employees.GetById.GetEmployeeByIdQuery,
but ServiceLocationPolicy.NotAllowed is in effect (it is the default).
Service EmployeeService.Application.Abstractions.IEmployeeRepository:
Dependency: ... EmployeeDbContext ... 
Dependency: ... DbContextOptions<EmployeeDbContext> ... 'opaque' lambda factory with the
Scoped lifetime and requires service location
```

Wolverines Codegen kann fuer Handler, die (transitiv ueber `IEmployeeRepository` ->
`EfEmployeeRepository` -> `EmployeeDbContext`) von der scoped `DbContextOptions<T>`-Factory
abhaengen, standardmaessig keinen kompilierten Aufrufcode erzeugen (`ServiceLocationPolicy.NotAllowed`
ist Wolverine-Default). **Warum bisher unentdeckt:** (1) In keinem der vier Services wird je eine
EF-Core-Migration automatisch angewendet (siehe Bug 2-Fix-Notiz), reale `_db`-Schemata waren daher
in der Vergangenheit fast nie befuellt/nutzbar; (2) alle Integrationstests
(`Tests/*.Tests/Web/*WebFactory.cs`) ersetzen das jeweilige `I*Repository` durch ein
Singleton-Fake OHNE eigene Abhaengigkeiten - das umgeht genau die DI-Kette, die den Fehler ausloest,
weshalb `dotnet test` durchgehend gruen bleibt. Da `CustomerService`/`AppointmentService`/
`IdentityService` denselben `EfXRepository(XDbContext)`-Aufbau + identisches `UseWolverine(...)`-
Muster verwenden (siehe deren `Program.cs`), ist derselbe Fehler dort mit hoher Wahrscheinlichkeit
ebenfalls reproduzierbar, sobald echte Migrationen angewendet sind - noch nicht einzeln verifiziert.
**Tragweite:** blockiert aktuell JEDEN echten End-to-End-Test von `employee-service`
(und vermutlich der anderen Services) gegen eine real migrierte Datenbank, unabhaengig von Bug 2 -
insbesondere `rbac.spec.ts` (Employee-Ownership-Erfolgspfad) kann trotz des Bug-2-Fixes nicht
gruen laufen, solange dieser Fehler besteht.
**Fix-Vorschlag (nicht in diesem Scope umgesetzt, da eigenstaendiges, tieferliegendes
Architekturthema - siehe `architect-agent`):** entweder Wolverine ueber
`opts.CodeGeneration.TypeLoadMode` / eine explizite `ServiceLocationPolicy`-Konfiguration erlauben,
diese Scoped-Dependency aufzuloesen, oder die `DbContextOptions<T>`-Registrierung so anpassen, dass
sie kein "opaque lambda factory"-Pattern mehr ist (z.B. durch einen eigenen
`IConfigureOptions<DbContextOptions<T>>`), oder Wolverine-Handler explizit mit
`[Transactional]`/eigener DI-Scope-Strategie versehen. Ausserdem: automatisches Anwenden der
vorhandenen EF-Core-Migrationen in `Development` fehlt in allen vier Services (nur fuer
`employee-service` jetzt als Nebeneffekt des Bug-2-Fixes im `DevEmployeeSeedHostedService`
ergaenzt, siehe oben) - sollte als eigenstaendiges Thema (devops-agent/backend-agent) behandelt
werden.

## Rate-Limiting-Hinweis (kein Bug, aber relevant fuer Testlaeufe)

Der Brute-Force-Schutz auf `POST /api/auth/login` (max. 5 Versuche/Minute/IP, siehe
`authentifizierung.openspec.md`) funktioniert **korrekt** (in dieser Session mehrfach
reproduziert: wiederholte Testlaeufe/manuelle `curl`-Aufrufe fuehrten zuverlaessig zu
`429 Too Many Requests`). Bei CI-Laeufen mit vielen aufeinanderfolgenden Tests, die alle
einen Login durchfuehren, kann dies zu Flakiness fuehren - ggf. die Rate-Limit-Konfiguration
fuer Test-/CI-Umgebungen ueber eine Umgebungsvariable grosszuegiger konfigurierbar machen
(nicht Teil dieses QA-Auftrags, nur als Hinweis fuer den naechsten Sprint).
