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
| `rabbitmq` | RabbitMQ-Container | Wolverine Messaging (Integration Events `CustomerDeactivated`, `EmployeeDeactivated`) |
| `customer-service` | Aspire-Projektressource | `Services/customer-service/src/CustomerService.Web` |
| `employee-service` | Aspire-Projektressource | `Services/employee-service/src/EmployeeService.Web` |
| `gateway` | Aspire-Projektressource | YARP-Reverse-Proxy (`Gateway/Crm.Gateway`), einziger extern erreichbarer HTTP-Endpunkt |

Der `appointment-service` ist noch nicht implementiert und daher (noch) nicht im AppHost verdrahtet;
er ist gemäß ADR 0001 als drittes Bounded Context vorgesehen und wird nachgezogen, sobald der
Service existiert.

## Starten

```bash
cd AppHost/Crm.AppHost
dotnet run
```

Der Aspire-Dashboard-Link wird in der Konsole ausgegeben (Standard: `https://localhost:17172`).
Dort sind alle Ressourcen, Logs, Traces und die dynamisch vergebenen Endpunkte einsehbar.

**Hinweis (lokale Entwicklung, Frontend):** Seit der Frontend-Konsolidierung
(`docs/adr/0002-frontend-konsolidierung.md`) existiert nur noch ein einziges Angular-Frontend
`Clients/crm-web` (Dev-Port `4200`) statt vormals drei getrennter Apps (`customer-web` 4200,
`employee-web` 4201, `appointment-web` 4202). `crm-web` spricht im Dev-Modus aktuell noch direkt
gegen die jeweiligen Services (`environment.ts`), im Produktions-Build bereits gegen den relativen
Pfad `/api` (für das Gateway). Sobald das Gateway lokal über Aspire läuft, ist es unter der vom
AppHost zugewiesenen externen HTTP-Endpoint-URL erreichbar (sichtbar im Aspire-Dashboard als
Endpunkt der Ressource `gateway`, z.B. `http://localhost:<zufälliger-port>`), mit Routen
`/api/customers/**` → `customer-service`, `/api/employees/**` → `employee-service` und
`/api/appointments/**` → `appointment-service`. Eine Umstellung der Angular-Dev-Configs auf das
Gateway ist eine separate Frontend-Aufgabe und nicht Teil dieser Änderung.

## Verifikation

- `dotnet build Crm.slnx` (Repo-Root) – kompiliert AppHost, ServiceDefaults, Gateway und beide
  Services fehlerfrei.
- `dotnet test` für `Tests/CustomerService.Tests` (35) und `Tests/EmployeeService.Tests` (40) –
  weiterhin grün nach Integration von `ServiceDefaults`.
- `dotnet run` des AppHosts wurde in dieser Umgebung tatsächlich ausgeführt (Docker Desktop war
  verfügbar): `customer-postgres`, `employee-postgres` und `rabbitmq` starten als Container,
  `customer-service`, `employee-service` und `gateway` erreichen den Zustand `Running` ohne Fehler
  in den Service-Logs. Ein Request über das Gateway wurde manuell verifiziert:
  - `GET /api/customers/{id}` und `GET /api/employees/{id}` über das Gateway liefern `401`
    (JWT-Auth-Termination greift, kein `502/503` – d.h. Service Discovery/Routing zu den
    Downstream-Services funktioniert).
  - `GET /health` über das Gateway liefert `200`, unbekannte Pfade liefern `404`.
  - Nach dem Test wurden alle Container/Prozesse wieder gestoppt.
