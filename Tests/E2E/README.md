# CRM E2E-Tests (Playwright)

End-to-End-Tests fuer die drei Kern-Capabilities des CRM (Kundenverwaltung, Mitarbeiterverwaltung,
Terminverwaltung inkl. RBAC) sowie den Authentifizierungs-Flow, abgeleitet direkt aus den
Scenarios (GIVEN/WHEN/THEN) in:

- `UserStories/kundenverwaltung.openspec.md`
- `UserStories/mitarbeiterverwaltung.openspec.md`
- `UserStories/terminverwaltung.openspec.md`
- `UserStories/authentifizierung.openspec.md`

## Warum Playwright (statt Cypress)

- Ein Testlauf muss ueber **drei unabhaengige Angular-Origins** (customer-web:4200,
  employee-web:4201, appointment-web:4202) hinweg funktionieren, inkl. echtem Cross-Origin-Login
  gegen `identity-service`. Playwright hat dafuer kein Origin-Lock-in (anders als Cypress'
  `cy.origin()`-Workaround) und startet pro Test einen frischen Browser-Context.
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
  `appointment-service`) UND die drei Angular-Dev-Server (`customer-web`, `employee-web`,
  `appointment-web`) laufen bereits. Diese Suite startet selbst **keine** Server
  (kein Playwright-`webServer`-Block), da in dieser Microservices-Landschaft zu viele
  unabhaengige Prozesse beteiligt sind.

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
  **Wichtig (siehe Bug-Report unten):** Beim aktuellen Stand bindet Aspire dynamische Ports
  UND sowohl HTTP als auch HTTPS auf jedem Service - dadurch erzwingt `UseHttpsRedirection()`
  in jedem Service einen 307-Redirect auf den **eigenen** Port, was den YARP-Gateway-Pfad und
  jede Inter-Service-Kommunikation ueber die vom AppHost zugewiesenen Adressen bricht. Diese
  Suite wurde deshalb tatsaechlich gegen einen **manuellen Einzelstart** verifiziert (naechster
  Punkt), nicht gegen den AppHost.

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
  Danach die drei Angular-Apps:
  ```bash
  cd Clients/customer-web && npx ng serve --port 4200
  cd Clients/employee-web && npx ng serve --port 4201
  cd Clients/appointment-web && npx ng serve --port 4202
  ```

Alle Basis-URLs (Web + API) sind zusaetzlich per Umgebungsvariable ueberschreibbar
(siehe `fixtures/env.ts`), falls Ports abweichen (z.B. `E2E_CUSTOMER_WEB_URL`,
`E2E_CUSTOMER_API_URL`, `E2E_ADMIN_PASSWORD`, ...).

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

### Bug 2 (P1, backend-agent/sec-agent): Keine Verknuepfung zwischen identity-service-User und employee-service-Employee

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

### Bug 3 (P1, devops-agent): AppHost bindet Services gleichzeitig auf HTTP und HTTPS -> UseHttpsRedirection bricht Gateway-Pfad

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

## Rate-Limiting-Hinweis (kein Bug, aber relevant fuer Testlaeufe)

Der Brute-Force-Schutz auf `POST /api/auth/login` (max. 5 Versuche/Minute/IP, siehe
`authentifizierung.openspec.md`) funktioniert **korrekt** (in dieser Session mehrfach
reproduziert: wiederholte Testlaeufe/manuelle `curl`-Aufrufe fuehrten zuverlaessig zu
`429 Too Many Requests`). Bei CI-Laeufen mit vielen aufeinanderfolgenden Tests, die alle
einen Login durchfuehren, kann dies zu Flakiness fuehren - ggf. die Rate-Limit-Konfiguration
fuer Test-/CI-Umgebungen ueber eine Umgebungsvariable grosszuegiger konfigurierbar machen
(nicht Teil dieses QA-Auftrags, nur als Hinweis fuer den naechsten Sprint).
