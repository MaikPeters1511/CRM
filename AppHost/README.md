# Crm.AppHost – Lokale Orchestrierung

Dieses Verzeichnis enthält die .NET Aspire Orchestrierung für das CRM gemäß
[`docs/adr/0001-microservices-architektur-crm.md`](../docs/adr/0001-microservices-architektur-crm.md).

## Projekte

- **`Crm.AppHost/`** – Aspire AppHost, startet und verdrahtet alle Ressourcen (Postgres-Container,
  RabbitMQ, Services, Gateway) über Service Discovery/`WithReference` – keine hartkodierten URLs.
- **`Crm.ServiceDefaults/`** – gemeinsames Aspire-Standardmuster (OpenTelemetry, Health-Checks,
  Service Discovery, Resilienz-Defaults für `HttpClient`), referenziert von `customer-service`,
  `employee-service` und dem Gateway.

## Orchestrierte Ressourcen

| Ressource | Typ | Zweck |
|---|---|---|
| `customer-postgres` → DB `customer-db` (Connection-String-Name `customer_db`) | PostgreSQL-Container | Eigene Datenbank für `customer-service` |
| `employee-postgres` → DB `employee-db` (Connection-String-Name `employee_db`) | PostgreSQL-Container | Eigene Datenbank für `employee-service` |
| `appointment-postgres` → DB `appointment-db` (Connection-String-Name `appointment_db`) | PostgreSQL-Container | Eigene Datenbank für `appointment-service` |
| `rabbitmq` | RabbitMQ-Container | Wolverine Messaging (Integration Events `CustomerDeactivated`, `EmployeeDeactivated`, `AppointmentCreated`/`Cancelled`/`Rescheduled`) |
| `identity-service` | Aspire-Projektressource | `Services/identity-service/src/IdentityService.Web` (JWT-Ausstellung, `POST /api/auth/login`) |
| `customer-service` | Aspire-Projektressource | `Services/customer-service/src/CustomerService.Web` |
| `employee-service` | Aspire-Projektressource | `Services/employee-service/src/EmployeeService.Web` |
| `appointment-service` | Aspire-Projektressource | `Services/appointment-service/src/AppointmentService.Web` (referenziert `customer-service`/`employee-service` per Service Discovery) |
| `gateway` | Aspire-Projektressource | YARP-Reverse-Proxy (`Gateway/Crm.Gateway`), einziger extern erreichbarer HTTP-Endpunkt für API-Traffic |
| `crm-web` | Aspire-JavaScript-App (`Aspire.Hosting.JavaScript`, `AddJavaScriptApp`) | Konsolidiertes Angular-Frontend (`Clients/crm-web`, ADR 0002), startet automatisch per `npm start` (`ng serve`), fester Port `4200` (`isProxied: false`, da der Angular-Dev-Server selbst an diesen Port bindet), wartet auf `gateway` |

Alle vier fachlichen Services sowie der Gateway sind seit der Umsetzung von
`terminverwaltung.openspec.md`/`authentifizierung.openspec.md` vollständig im AppHost verdrahtet
(siehe `AppHost/Crm.AppHost/AppHost.cs`). Ein gemeinsames JWT-Schema (Issuer/Audience/
Signing-Key, siehe Aspire-Parameter `jwt-issuer`/`jwt-audience`/`jwt-signing-key`) wird an
`identity-service` (Aussteller) und Gateway/`customer-service`/`employee-service`/
`appointment-service` (Validierer, Defense in Depth) verteilt.

## Starten

```bash
cd AppHost/Crm.AppHost
dotnet run
```

Der Aspire-Dashboard-Link wird in der Konsole ausgegeben (Standard: `https://localhost:17172`).
Dort sind alle Ressourcen, Logs, Traces und die dynamisch vergebenen Endpunkte einsehbar.

**Frontend (`crm-web`):** Startet automatisch mit `dotnet run` im AppHost (keine separate
`ng serve` nötig). Benötigt `npm`/`node` im `PATH` (Aspire löst beide Kommandos beim Start auf
und bricht sonst ab). Dev-Modus spricht aktuell noch direkt gegen die jeweiligen Services
(`environment.ts`, feste Ports 5149/5159/5169/5179 je Service), im Produktions-Build gegen den
relativen Pfad `/api` (Gateway). Eine Umstellung der Dev-Config auf das Gateway ist eine separate
Frontend-Aufgabe. Die Gateway-CORS-Allowlist (`Gateway/Crm.Gateway/appsettings.json`) erlaubt
ausschließlich `http://localhost:4200`, passend zum festen `crm-web`-Dev-Port.

## Verifikation

- `dotnet build Crm.slnx` (Repo-Root) – kompiliert AppHost, ServiceDefaults, Gateway, alle vier
  Services und (separat, siehe Hinweis zu `.esproj` unten) `crm-web` fehlerfrei.
- `dotnet test Crm.slnx` – 140/140 Tests grün.
- `dotnet run` des AppHosts wurde real ausgeführt (Docker Desktop verfügbar): alle drei
  Postgres-Container und RabbitMQ starten, alle vier Services, `gateway` und `crm-web` erreichen
  den Zustand `Running` (nicht `Finished`) ohne Fehler in den Logs.
  - `crm-web` antwortet unter `http://localhost:4200` mit `200`.
  - `gateway` antwortet unter `/health` mit `200`; `GET /api/customers` ohne Token liefert `401`
    (Routing/Auth-Termination funktioniert, kein `502/503`).
  - Nach dem Test wurden alle Container/Prozesse wieder gestoppt.
- **Stolperstein (gelöst):** `AddJavaScriptApp(...).WithHttpEndpoint(port: 4200)` ohne
  `isProxied: false` lässt Aspires DCP-Proxy denselben Port belegen, den der Angular-Dev-Server
  selbst (laut `angular.json`, `serve.options.port`) binden will – der `npm start`-Prozess stirbt
  dann sofort mit `Port 4200 is already in use`. Fix: `isProxied: false`, damit DCP den Port nicht
  selbst reserviert.

## Hinweis: `crm-web.esproj`

`Clients/crm-web/crm-web.esproj` bindet die Angular-App zusätzlich als Projekt in `Crm.slnx` ein
(für IDE-Tooling, z.B. Visual Studio). `.esproj`-Projekte sind in der offiziellen Projekttyp-Tabelle
mit `NoBuildRules` registriert und daher standardmäßig von einem normalen Solution-Build
ausgenommen – das ist kein Fehler. Die Aspire-Orchestrierung oben (`AddJavaScriptApp`) ist davon
unabhängig und läuft unabhängig vom `.esproj`.
