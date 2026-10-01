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

## Tatsaechliche Verifikation - finaler Stand (qa-agent, 2026-09-30, nach Bug-1/2/3/4-Fixes)

**Setup:** Kompletter Stack real hochgefahren - AppHost fuer identity-/customer-/employee-/
appointment-service + 3x Postgres + RabbitMQ (`dotnet run --project AppHost/Crm.AppHost`,
vorhandene User Secrets), zusaetzlich manuell `npx ng serve --port 4200` fuer `Clients/crm-web`
(die konsolidierte Angular-App ist NICHT im AppHost registriert - das ist separat von den
Backend-Services zu starten, siehe Setup-Abschnitt oben). `customer_db`/`appointment_db` mussten
vor dem ersten Request einmalig per `dotnet ef database update` migriert werden (siehe Bug 5) -
danach lief die Suite mehrfach vollstaendig durch.

**Ergebnis (letzter vollstaendiger Lauf, mehrere Wiederholungen zur Stabilitaetspruefung
durchgefuehrt):** **16 von 30 Tests gruen, 14 rot** - und zwar **stabil und deterministisch
dieselben 14**, quer ueber mehrere Wiederholungen (nicht flockend/zufaellig). Jeder der 14 roten
Tests scheitert ausschliesslich an einem der zwei noch offenen, echten Backend-Bugs (Bug 6 und
Bug 7, siehe unten) - keiner der 14 roten Tests hat einen Fehler im Testcode selbst.

- **Gruen (16):** `auth.spec.ts` alle 6/6; `customers.spec.ts` alle 6/6; `employees.spec.ts`
  4/7 (Anlegen gueltig, E-Mail-Konflikt, Leistung zuordnen, Deaktivieren).
- **Rot (14), alle auf Bug 6/7 zurueckfuehrbar:**
  - `employees.spec.ts`: "Wochenarbeitszeit ... hinterlegen" (Bug 6), "Urlaub fuer einen
    terminfreien Zeitraum eintragen" (Bug 7), "Urlaub kollidiert mit bestehendem Termin" (Bug 7).
  - `appointments.spec.ts` (alle 6 Tests) und `rbac.spec.ts` (alle 5 Tests): jeder Test ruft in
    seiner Testdaten-Vorbereitung `setupEmployeeWithMondaySchedule()`/
    `setupEmployeeWithAppointment()` auf, die intern `setWorkingHours()` aufruft - dieser API-Call
    schlaegt wegen Bug 6 IMMER fehl, wodurch keiner dieser 11 Tests ueberhaupt bis zur eigentlichen
    UI-Interaktion kommt.
- Auf dem Weg zu diesem Ergebnis wurden mehrere **eigene Testcode-Fehler** identifiziert und
  behoben (kein Produktbug, Details siehe Commit-Historie/Diff dieser Session):
  veraltete/falsche i18n-Texterwartungen ("Ansehen" statt "Anzeigen", "Deaktivieren"/"Stornieren"
  statt generischem "Bestaetigen" fuer Modal-Bestaetigungs-Buttons - jeweils per `role="dialog"`-
  Scoping disambiguiert), zwei `getByText(lastName)`-Strict-Mode-Verstoesse (Name- und E-Mail-Zelle
  matchen beide), ein SPA-Navigations-Race unmittelbar nach Routerlink-Klicks (Playwright `fill()`
  kurz nach der Navigation kann von der noch nicht vollstaendig gebundenen Reactive-Form-Instanz
  verloren gehen - behoben durch `fixtures/ui.ts#settleAfterNavigation`), die seit der
  Frontend-Konsolidierung falsche Annahme, Login wuerde je nach `*Web`-Alias auf einen anderen
  Feature-Bereich umleiten (tut es nicht mehr - Default ist immer `/customers`), sowie zwei
  ungueltige `serviceId`-Werte in Testdaten (muss laut `EmployeeServiceAssignmentDto` eine Guid
  sein, kein sprechender String). Zusaetzlich wurde ein Retry-mit-Wartezeit fuer den (korrekt
  funktionierenden) Login-Rate-Limiter (5/Minute/IP) in `fixtures/auth.ts`/`fixtures/api.ts`
  ergaenzt, da ein sequentieller Lauf aller 30 Tests dieses Limit zuverlaessig ueberschreitet.
- **Bug 2** (User<->Employee-Verknuepfung) ist vollstaendig verifiziert: der zuvor
  `test.fixme`-markierte Ownership-Positivtest in `rbac.spec.ts` ist jetzt ein echter,
  aktiver Test (aktuell rot NUR wegen Bug 6, nicht wegen Bug 2 - die Verknuepfung selbst
  funktioniert, siehe Login-Response `userId` == geseedete `Employee.Id`).
- **Fazit:** Kein einziger roter Test in diesem finalen Stand ist ein Testcode-Problem - die
  Suite ist korrekt geschrieben; 14 Tests koennen erst nach Bug 6 UND Bug 7 (beide backend-agent)
  gruen werden. Empfehlung: nach deren Fix diesen Lauf wiederholen - erwartet werden dann
  30/30 gruen (kein weiterer bekannter Blocker).

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

### Bug 2 (P1, backend-agent/sec-agent): Keine Verknuepfung zwischen identity-service-User und employee-service-Employee — BEHOBEN, VOLLSTAENDIG VERIFIZIERT

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

**Update (qa-agent, finaler Verifikationslauf, 2026-09-30):** Nach dem Bug-4-Fix wurde das
`test.fixme(...)` in `rbac.spec.ts` durch einen echten, aktiven Test ersetzt
("Employee sieht eigene Termine in der Kalenderansicht (Ownership-Positivfall, Bug 2 behoben)").
Der reine Ownership-Mechanismus selbst (User-Id == Employee-Id) ist damit vollstaendig verifiziert -
der Test scheitert aktuell NICHT an Bug 2, sondern ausschliesslich an Bug 6 (`setWorkingHours()`
in der Testdaten-Vorbereitung, siehe unten). Bug 2 gilt damit als vollstaendig geschlossen.

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

### Bug 4 (P0, backend-agent): Wolverine-Codegen scheitert fuer Handler mit scoped EF-Core-Repository gegen echte Datenbank — BEHOBEN

**Status:** Behoben (backend-agent, 2026-09-30). Ursache: `AddDbContext<TDbContext>()` registriert
`DbContextOptions<TDbContext>` als scoped "opaque lambda factory"
(`EntityFrameworkServiceCollectionExtensions.CreateDbContextOptions`); Wolverines Codegen kann
daraus per Konstruktor-Injektion keinen Aufrufcode erzeugen und verlangt Service Location, was seit
Wolverine 6.0 per Default (`ServiceLocationPolicy.NotAllowed`) verboten ist. Fix laut
Wolverine-Dokumentation (https://wolverinefx.net/guide/codegen.html, Abschnitt "Service Location"):
genau den betroffenen Service gezielt fuer Service Location freischalten, statt die Policy global
zu lockern - `opts.CodeGeneration.AlwaysUseServiceLocationFor<T>()` in der jeweiligen
`UseWolverine(...)`-Konfiguration. Betroffen und behoben: `employee-service`
(`Services/employee-service/src/EmployeeService.Web/Program.cs`, `IEmployeeRepository`),
`customer-service` (`Services/customer-service/src/CustomerService.Web/Program.cs`,
`ICustomerRepository`), `appointment-service`
(`Services/appointment-service/src/AppointmentService.Web/Program.cs`, `IAppointmentRepository`) -
alle drei nutzen identisches `EfXRepository(XDbContext)` + `AddDbContext<XDbContext>`-Muster.
`identity-service` ist NICHT betroffen (`IUserRepository` ist ein Singleton-In-Memory-Repository
ohne `AddDbContext`/scoped Dependency). Real verifiziert: fuer alle drei Services wurde der Bug
zunaechst gegen eine echte, per `dotnet ef database update` migrierte lokale PostgreSQL (kein Fake)
reproduziert (`500` + `Wolverine.Configuration.InvalidServiceLocationException` im Log, identisch
zur unten dokumentierten Fehlermeldung), dann der Fix eingespielt und derselbe Request erneut
gegen dieselbe Datenbank gestellt: `GET /api/employees`, `GET /api/employees/{id}`,
`GET /api/customers`, `GET /api/appointments` liefern jetzt alle real `200` mit korrekten Daten
(u.a. der von `DevEmployeeSeedHostedService` geseedete `employee@crm.local`-Datensatz). Zusaetzlich
neue Regressionstests, die - anders als alle bisherigen Tests - die echte EF-Core-Infrastruktur
(kein Fake-Repository) ueber Testcontainers-PostgreSQL durchlaufen:
`Tests/EmployeeService.Tests/Web/EmployeesRealInfrastructureApiTests.cs`,
`Tests/CustomerService.Tests/Web/CustomersRealInfrastructureApiTests.cs`,
`Tests/AppointmentService.Tests/Web/AppointmentsRealInfrastructureApiTests.cs` (jeweils mit
zugehoeriger `*DbContainerFixture`/`*RealInfrastructureWebFactory` in `Support`/`Web`).
`dotnet build Crm.slnx`/`dotnet test Crm.slnx` (132 Tests) bleiben gruen. Die urspruengliche
Bug-Beschreibung unten bleibt zur Nachvollziehbarkeit unveraendert erhalten.

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

### Bug 5 (P0, devops-agent/backend-agent): customer-service und appointment-service wenden EF-Core-Migrationen beim Start NICHT automatisch an -> jeder Request gegen eine frische Postgres-Instanz schlaegt mit 500 fehl — BEHOBEN

**Status:** Behoben (backend-agent, 2026-10-01). Neue, pro Service identische
`DatabaseMigrationHostedService<TDbContext>`-Klasse (`Services/customer-service/src/CustomerService.Infrastructure/Persistence/`,
`Services/appointment-service/src/AppointmentService.Infrastructure/Persistence/`,
`Services/employee-service/src/EmployeeService.Infrastructure/Persistence/`), registriert
UNCONDITIONAL (jede Umgebung, nicht nur Development - kein separater Migrations-Deployment-Schritt
in dieser Landschaft) in den jeweiligen `AddXServiceInfrastructure`-Methoden; wendet beim Start
`dbContext.Database.MigrateAsync()` an, Fehler werden geloggt, verhindern den Service-Start aber
nicht (z.B. Integrationstests mit Fake-Repository/ungueltigem Connection-String). Bewusst getrennt
vom reinen Development-only Daten-Seeding (Bug 2, `DevEmployeeSeedHostedService`), aus dem die
bisher dort "nebenbei" laufende Migration entfernt wurde. Real verifiziert: frischer,
nicht-migrierter Postgres-Container (`docker run postgres:16-alpine`, leere `customer_db`),
`customer-service` dagegen gestartet (`ASPNETCORE_ENVIRONMENT=Development`,
`ConnectionStrings__customer_db` auf die leere DB zeigend) - Log zeigt automatisches
`Applying migration '20260929103501_InitialCreate'`, danach `\dt` bestaetigt die angelegten
Tabellen (`customers`, `__EFMigrationsHistory`); `GET /api/customers` liefert danach `401`
(Auth-Pruefung erreicht, kein `500` mehr). `appointment-service`/`employee-service` nutzen
identischen Code (gleiche Klasse, gleiches Registrierungsmuster) und werden zusaetzlich indirekt
durch die Testcontainers-basierten `*RealInfrastructureApiTests` (frischer Postgres-Container pro
Testlauf, keine manuelle Migration) abgedeckt. `dotnet build Crm.slnx`/`dotnet test Crm.slnx`
(134 Tests) bleiben gruen. Die urspruengliche Bug-Beschreibung unten bleibt zur
Nachvollziehbarkeit unveraendert erhalten.

**Status (alt):** offen, nicht gefixt (qa-agent, 2026-09-30, im finalen Verifikationslauf entdeckt).
Bereits in Bug 4 als bekannte Luecke vermerkt ("automatisches Anwenden der vorhandenen
EF-Core-Migrationen in Development fehlt in allen vier Services... nur fuer employee-service
jetzt als Nebeneffekt des Bug-2-Fixes ergaenzt"), hier real reproduziert und als eigener
Bug-Eintrag festgehalten, weil er den finalen E2E-Lauf tatsaechlich blockiert hat.

**Reproduktion:** `AppHost/Crm.AppHost` frisch gestartet (neue, nicht-persistente
Postgres-Container fuer `customer-postgres`/`appointment-postgres`/`employee-postgres`, wie es
bei jedem `dotnet run`/CI-Lauf ohne Docker-Volume passiert). Danach:
```
GET http://localhost:5149/api/customers?page=1   -> 500 {"title":"An unexpected error occurred."...}
GET http://localhost:5169/api/appointments       -> (analog, ungetestete Variante ohne Migration)
```
`psql` gegen den laufenden Container bestaetigt: `customer_db` und `appointment_db` haben
**keine Tabellen** (`\dt` liefert "Did not find any tables"), waehrend `employee_db` dank
`DevEmployeeSeedHostedService.StartAsync()` (das dort beilaeufig `dbContext.Database.MigrateAsync()`
aufruft, siehe Bug-2-Fix) korrekt 5 Tabellen inkl. `__EFMigrationsHistory` hat. Nach manuellem
`dotnet ef database update` (nur als lokaler Verifikations-Workaround fuer diesen Testlauf, KEINE
Aenderung an Produktivcode) gegen beide Datenbanken funktionieren beide Endpunkte sofort
(`200`, korrekte Daten) - die Ursache ist zweifelsfrei die fehlende Migration, kein sonstiger
Code-Defekt in `SearchCustomersHandler`/`AppointmentsController`.

**Auswirkung:** Blockierte in diesem Testlauf zunaechst alle 5 Tests in `customers.spec.ts` und
alle 6 Tests in `appointments.spec.ts` (sowie transitiv `rbac.spec.ts`, das Termine ueber dieselbe
API anlegt) mit demselben Symptom wie in `customer-form.component.ts`: die Kundenliste
(`CustomerStateService.loadCustomers()`) schlaegt beim Betreten von `/customers` fehl und setzt
einen globalen `hasErrors()`-Zustand ("Die Kundendaten konnten nicht geladen werden."), der beim
anschliessenden Navigieren zu `/customers/new` weiterhin angezeigt wird (das Formular selbst laedt
in diesem Fall nichts, zeigt aber den stehengebliebenen Fehler aus dem State-Service - kein
zusaetzlicher Frontend-Bug, nur eine Folge des Backend-500). Jeder CI-Lauf/Onboarding, der den
AppHost ohne vorheriges manuelles `dotnet ef database update` frisch startet, ist betroffen -
nicht nur diese Testsuite.

**Fix-Vorschlag:** analog zum bereits vorhandenen Muster in `EmployeeService.Infrastructure.Seed.
DevEmployeeSeedHostedService` (`dbContext.Database.MigrateAsync()`, mit Try/Catch, das den
Service-Start nicht blockiert) einen entsprechenden, minimalen `IHostedService`/Startup-Schritt
auch in `customer-service` und `appointment-service` ergaenzen - idealerweise als gemeinsames,
wiederverwendbares Muster (nicht dreifach copy-paste), z.B. eine kleine gemeinsame
`EnsureDatabaseMigratedHostedService<TDbContext>`-Hilfsklasse, NUR fuer `Development` registriert.
Alternativ/ergaenzend (devops-agent): Migrationen als expliziten Schritt im AppHost/CI-Pipeline
vor dem eigentlichen Testlauf ausfuehren.

### Bug 6 (P0, backend-agent): `PUT /employees/{id}/schedule` schlaegt beim ERSTEN Setzen der Arbeitszeit mit `DbUpdateConcurrencyException` fehl (EF Core generiert UPDATE statt INSERT) — BEHOBEN

**Status:** Behoben (backend-agent, 2026-10-01). `EmployeeDbContext.OnModelCreating`:
`workingHoursBuilder.Property(w => w.Id).ValueGeneratedNever();` ergaenzt - exakt wie im
Fix-Vorschlag. Beim Review wurde zusaetzlich `absencesBuilder` auf dasselbe Muster geprueft (dort
bereits als wahrscheinlich betroffen vermerkt) und ebenfalls mit `ValueGeneratedNever()` fuer
`Absence.Id` korrigiert, um denselben Fehler fuer Abwesenheiten vorsorglich zu vermeiden. Real
verifiziert: `Tests/EmployeeService.Tests/Web/DebugScheduleTests.cs`,
`SetWorkingHours_FirstTimeForEmployee_ReturnsOkInsteadOfConcurrencyException` laeuft gegen eine
echte, frische Testcontainers-PostgreSQL und liefert jetzt `200 OK` (vorher `500` mit
`DbUpdateConcurrencyException`). `dotnet build Crm.slnx`/`dotnet test Crm.slnx` (134 Tests)
bleiben gruen. Die urspruengliche Bug-Beschreibung unten bleibt zur Nachvollziehbarkeit
unveraendert erhalten.

**Status (alt):** offen, nicht gefixt (qa-agent, 2026-09-30, im finalen Verifikationslauf entdeckt und
per Testcontainers-Regressionstest verifiziert).

**Reproduktion:** Neuen Mitarbeiter anlegen (leere `WorkingHours`-Collection), danach
`PUT /api/employees/{id}/schedule` mit einem einzigen Slot aufrufen:
```
PUT /api/employees/{id}/schedule  {"slots":[{"weekday":"Monday","startTime":"09:00:00","endTime":"17:00:00"}]}
-> 500 {"title":"An unexpected error occurred."...}
```
Server-Log (`GlobalExceptionHandler`):
```
Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: The database operation was expected
to affect 1 row(s), but actually affected 0 row(s); ...
UPDATE employee_working_hours SET "EmployeeId" = @p0, "EndTime" = @p1, "StartTime" = @p2, "Weekday" = @p3
WHERE "Id" = @p4;
```
EF Core generiert fuer die neu erzeugte `WorkingHours`-Instanz (`Employee.SetWorkingHours()`:
`_workingHours.Clear()` + `_workingHours.Add(new WorkingHours(Guid.NewGuid(), ...))`) ein UPDATE
statt eines INSERT, weil `EmployeeDbContext.OnModelCreating` fuer das Owned-Type
`Employee.WorkingHours` einen eigenen Surrogatschluessel definiert
(`workingHoursBuilder.HasKey(w => w.Id)`), dessen `Id`-Property client-seitig per `Guid.NewGuid()`
VOR dem Speichern gesetzt wird, aber OHNE `.ValueGeneratedNever()`. EF Core geht bei Guid-PKs
standardmaessig von `ValueGeneratedOnAdd` aus und interpretiert einen bereits gesetzten,
nicht-leeren Guid-Wert als "Entity existiert schon in der Datenbank" (`IsKeySet`-Heuristik) -
obwohl es sich um eine brandneue, dem DbContext noch nie bekannte Zeile handelt. Ergebnis:
EF versucht ein UPDATE auf eine nicht existierende Zeile -> 0 betroffene Zeilen -> Concurrency-
Exception. `EmployeeServiceAssignment` (Leistungszuordnung, `ReplaceServices()`, identisches
Clear()+Add()-Muster) ist NICHT betroffen, weil dort kein eigener Surrogatschluessel existiert
(`HasKey("EmployeeId", nameof(EmployeeServiceAssignment.ServiceId))`, rein zusammengesetzt aus
bereits bekannten Werten) - das erklaert, warum "Leistung zuordnen" in dieser Session funktioniert,
"Wochenarbeitszeit hinterlegen" aber nicht.

**Regressionstest (real gegen Testcontainers-PostgreSQL, kein Fake-Repository):**
`Tests/EmployeeService.Tests/Web/DebugScheduleTests.cs`,
`ScheduleAndAbsenceRealInfrastructureApiTests.SetWorkingHours_FirstTimeForEmployee_ReturnsOkInsteadOfConcurrencyException`
(aktuell rot - dokumentiert den Bug, wird erst nach dem Fix gruen erwartet).

**Auswirkung:** Blockiert `Tests/E2E/tests/employees.spec.ts` ("Wochenarbeitszeit Mo-Fr 09:00-17:00
hinterlegen") sowie transitiv JEDEN Test in `appointments.spec.ts`/`rbac.spec.ts`, der ueber
`setWorkingHours()` (siehe `fixtures/api.ts`) eine Arbeitszeit fuer einen frisch angelegten
Mitarbeiter benoetigt (Vorbedingung fuer "Termin im Zeitfenster anlegen" etc.) - de facto der
Hauptblocker fuer alle drei Spec-Dateien in diesem Verifikationslauf.

**Fix-Vorschlag:** in `EmployeeDbContext.OnModelCreating`, `workingHoursBuilder`, ergaenzen:
`workingHoursBuilder.Property(w => w.Id).ValueGeneratedNever();` (die Absence-Konfiguration
`absencesBuilder.HasKey(a => a.Id)` hat exakt dasselbe Muster und ist mit hoher Wahrscheinlichkeit
ebenso betroffen - siehe Bug 7, wo die Absence-Erstellung aus einem anderen Grund ohnehin schon
scheitert, sodass dieser Effekt dort noch nicht sichtbar wurde; nach Beheben von Bug 7 bitte
erneut pruefen).

### Bug 7 (P0, backend-agent): `POST /employees/{id}/absences` scheitert IMMER mit `InvalidServiceLocationException` fuer `IAppointmentAvailabilityClient` - Bug-4-Fix war unvollstaendig — BEHOBEN

**Status:** Behoben (backend-agent, 2026-10-01). `opts.CodeGeneration.AlwaysUseServiceLocationFor<IAppointmentAvailabilityClient>()`
in `Services/employee-service/src/EmployeeService.Web/Program.cs` ergaenzt (identisches Muster
wie der Bug-4-Fix fuer `IEmployeeRepository`). Beim Review (Grep nach injizierten
Interfaces in allen Application-Handlern aller drei Services) wurde derselbe
"opaque lambda factory"-Registrierungsmechanismus (`AddHttpClient<TClient, TImplementation>`)
auch bei `ICustomerVerificationClient`/`IEmployeeAvailabilityClient` in `appointment-service`
(`CreateAppointmentHandler`/`RescheduleAppointmentHandler`) gefunden - vorsorglich im selben Zug
behoben (`Services/appointment-service/src/AppointmentService.Web/Program.cs`), da identischer
Mechanismus, identisches Risiko. Zusaetzlich beim Verifizieren des Fixes eine direkt anschliessende
Folgeursache entdeckt und behoben: `Services:AppointmentService:BaseUrl`/`Services:CustomerService:BaseUrl`/
`Services:EmployeeService:BaseUrl` sind in den jeweiligen `appsettings.json` bewusst als leerer
String vorbelegt (Platzhalter fuer Environment-Variable/Aspire) - ein simples `??` greift fuer
einen bereits vorhandenen, aber leeren String NICHT, was zu einer `UriFormatException` statt dem
beabsichtigten Fallback fuehrte, sobald die Service-Location-Aufloesung ueberhaupt erst bis zu
dieser Stelle kommt (vorher durch die `InvalidServiceLocationException` verdeckt). Fix:
`IsNullOrWhiteSpace`-Pruefung statt `??` in allen drei betroffenen `AddXClient`-Methoden
(`EmployeeService.Infrastructure.DependencyInjection`, `AppointmentService.Infrastructure.DependencyInjection`).
Real verifiziert: `Tests/EmployeeService.Tests/Web/DebugScheduleTests.cs`,
`AddAbsence_ReturnsOkOrConflictInsteadOfInvalidServiceLocationException` (ergaenzt um einen
minimalen lokalen HTTP-Stub fuer `appointment-service`, da in der Testumgebung kein echter
appointment-service laeuft) liefert jetzt `200 OK` statt `500`. `dotnet build Crm.slnx`/
`dotnet test Crm.slnx` (134 Tests) bleiben gruen. Die urspruengliche Bug-Beschreibung unten
bleibt zur Nachvollziehbarkeit unveraendert erhalten.

**Status (alt):** offen, nicht gefixt (qa-agent, 2026-09-30, im finalen Verifikationslauf entdeckt und
per Testcontainers-Regressionstest verifiziert).

**Reproduktion:** Neuen Mitarbeiter anlegen, danach `POST /api/employees/{id}/absences`:
```
POST /api/employees/{id}/absences  {"type":"Vacation","from":"2027-01-04","to":"2027-01-08"}
-> 500 {"title":"An unexpected error occurred."...}
```
Server-Log:
```
Wolverine.Configuration.InvalidServiceLocationException: Found service locations while generating
code for Message Handler for EmployeeService.Application.Employees.AddAbsence.AddAbsenceCommand,
but ServiceLocationPolicy.NotAllowed is in effect (it is the default).
Service EmployeeService.Application.Abstractions.IAppointmentAvailabilityClient: The service
registration for ... is an 'opaque' lambda factory with the Transient lifetime and requires
service location
```
Dieselbe Fehlerklasse wie der bereits behobene Bug 4, aber fuer einen ANDEREN Service:
`IAppointmentAvailabilityClient` (wird fuer die Abwesenheits-Konfliktpruefung gegen
appointment-service benoetigt, siehe `AddAbsenceHandler`). Der Bug-4-Fix
(`opts.CodeGeneration.AlwaysUseServiceLocationFor<T>()` in
`Services/employee-service/src/EmployeeService.Web/Program.cs`) deckt bisher nur
`IEmployeeRepository` ab - `IAppointmentAvailabilityClient` wurde nicht ergaenzt und ist mit
demselben "opaque lambda factory"-Registrierungsmuster (Transient statt Scoped, aber dasselbe
grundsaetzliche Problem fuer Wolverines Codegen) betroffen.

**Regressionstest (real gegen Testcontainers-PostgreSQL):**
`Tests/EmployeeService.Tests/Web/DebugScheduleTests.cs`,
`ScheduleAndAbsenceRealInfrastructureApiTests.AddAbsence_ReturnsOkOrConflictInsteadOfInvalidServiceLocationException`
(aktuell rot).

**Auswirkung:** Blockiert JEDE Abwesenheitserfassung (`employees.spec.ts`: "Urlaub fuer einen
terminfreien Zeitraum eintragen", "Urlaub kollidiert mit bestehendem Termin") vollstaendig -
unabhaengig von Bug 6, da dieser Fehler schon vor dem ersten Speicherversuch beim Wolverine-Codegen
auftritt.

**Fix-Vorschlag:** analog zum Bug-4-Fix in `Services/employee-service/src/EmployeeService.Web/Program.cs`
ergaenzen: `opts.CodeGeneration.AlwaysUseServiceLocationFor<EmployeeService.Application.Abstractions.IAppointmentAvailabilityClient>();`
Danach bitte pruefen, ob nach diesem Fix Bug 6 fuer den Absence-Pfad (`absencesBuilder.HasKey(a => a.Id)`,
identisches Owned-Type-Muster wie `WorkingHours`) ebenfalls sichtbar wird.

## Rate-Limiting-Hinweis (kein Bug, aber relevant fuer Testlaeufe)

Der Brute-Force-Schutz auf `POST /api/auth/login` (max. 5 Versuche/Minute/IP, siehe
`authentifizierung.openspec.md`) funktioniert **korrekt** (in dieser Session mehrfach
reproduziert: wiederholte Testlaeufe/manuelle `curl`-Aufrufe fuehrten zuverlaessig zu
`429 Too Many Requests`). Bei CI-Laeufen mit vielen aufeinanderfolgenden Tests, die alle
einen Login durchfuehren, kann dies zu Flakiness fuehren - ggf. die Rate-Limit-Konfiguration
fuer Test-/CI-Umgebungen ueber eine Umgebungsvariable grosszuegiger konfigurierbar machen
(nicht Teil dieses QA-Auftrags, nur als Hinweis fuer den naechsten Sprint).
