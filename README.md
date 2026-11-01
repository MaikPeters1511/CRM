# CRM

Ein agentengesteuert entwickeltes CRM auf Basis von Microservices (.NET 10 / C# 14), einer
konsolidierten Angular-22-Anwendung und .NET Aspire als lokaler Orchestrierung.

## Systemübersicht

| Komponente | Pfad | Zweck | Port (Dev, Direct) |
|---|---|---|---|
| `identity-service` | `Services/identity-service/` | Authentifizierung: schlanker, selbst gehosteter JWT-Issuer (`POST /api/auth/login`), Seed-Nutzer Admin/Employee | `http://localhost:5179` |
| `customer-service` | `Services/customer-service/` | Kundenverwaltung (CRUD, Suche, Soft-Delete) + eigene DB `customer_db` | `http://localhost:5149` |
| `employee-service` | `Services/employee-service/` | Mitarbeiterverwaltung (Rollen, Leistungen, Arbeitszeiten, Abwesenheiten, Verfügbarkeit) + eigene DB `employee_db` | `http://localhost:5159` |
| `appointment-service` | `Services/appointment-service/` | Terminverwaltung (Anlegen/Verschieben/Stornieren, Kollisionsprüfung, Kalender, RBAC) + eigene DB `appointment_db` | `http://localhost:5169` |
| `catalog-service` | `Services/catalog-service/` | Leistungskatalog (Anlegen/Bearbeiten/Deaktivieren von Leistungen für die Mitarbeiter-Zuordnung) + eigene DB `catalog_db` | `http://localhost:5189` |
| `Gateway/Crm.Gateway` | `Gateway/Crm.Gateway/` | YARP-Reverse-Proxy, einziger öffentlicher HTTP-Eintrittspunkt, Auth-Termination (JWT) | dynamisch (Aspire) |
| `Clients/crm-web` | `Clients/crm-web/` | Eine konsolidierte Angular-22-Standalone-Anwendung (Kunden/Mitarbeiter/Termine/Login) | `http://localhost:4200` |
| `AppHost/Crm.AppHost` | `AppHost/` | .NET Aspire AppHost: orchestriert alle Services, 3× PostgreSQL, RabbitMQ, Gateway | Aspire-Dashboard |

Jeder Backend-Service ist intern nach Clean Architecture (Domain/Application/Infrastructure/Web)
geschichtet und besitzt sein eigenes Swagger/OpenAPI-Dokument unter `/swagger` (nur in
`Development`, siehe jeweiliges `Program.cs`). Die Services kommunizieren synchron (Polly-
abgesicherte REST-Calls für Validierungen) bzw. asynchron (Wolverine/RabbitMQ Integration Events)
untereinander — siehe Architekturentscheidungen unten.

## Architekturentscheidungen (ADRs)

- [`docs/adr/0001-microservices-architektur-crm.md`](docs/adr/0001-microservices-architektur-crm.md) —
  Servicegrenzen, Kommunikationsmuster (synchron/asynchron), Datenkonsistenzstrategie, API-Gateway,
  Resilienz, interne Clean Architecture je Service, Aspire-Orchestrierung.
- [`docs/adr/0002-frontend-konsolidierung.md`](docs/adr/0002-frontend-konsolidierung.md) —
  Eine gemeinsame Angular-Anwendung (`crm-web`) statt eines separaten Clients pro Backend-Service.

## Fachliche Spezifikationen (OpenSpec)

Alle Anforderungen folgen dem [OpenSpec](https://openspec.dev/docs/)-Workflow (propose → apply →
archive) in `UserStories/`. Alle sechs bisherigen Change-Specs sind bereits vollständig
abgenommen und archiviert; die dauerhaft gültigen Requirements liegen in `UserStories/specs/`:

- [`UserStories/specs/customer-management.openspec.md`](UserStories/specs/customer-management.openspec.md)
- [`UserStories/specs/employee-management.openspec.md`](UserStories/specs/employee-management.openspec.md)
- [`UserStories/specs/appointment-scheduling.openspec.md`](UserStories/specs/appointment-scheduling.openspec.md)
- [`UserStories/specs/identity.openspec.md`](UserStories/specs/identity.openspec.md)
- [`UserStories/specs/frontend-shell.openspec.md`](UserStories/specs/frontend-shell.openspec.md)
- [`UserStories/specs/service-catalog.openspec.md`](UserStories/specs/service-catalog.openspec.md)

Die ursprünglichen Change-Specs (Proposal/Design/Tasks-Historie) liegen in `UserStories/archive/`.
Eine neue Anforderung entsteht als neue Change-Spec direkt in `UserStories/`.

## Lokales Setup

**Voraussetzungen:** .NET 10 SDK, Node.js 20+, Docker Desktop (für PostgreSQL/RabbitMQ-Container,
lokal über Aspire orchestriert).

```bash
# 1. Secrets setzen (einmalig, siehe AppHost/README.md für Details)
cd AppHost/Crm.AppHost
dotnet user-secrets set "Parameters:jwt-signing-key" "$(openssl rand -base64 32)"
dotnet user-secrets set "Parameters:identity-admin-password" "Admin123!"
dotnet user-secrets set "Parameters:identity-employee-password" "Employee123!"

# 2. Alles starten (identity-/customer-/employee-/appointment-/catalog-service, Gateway,
#    4× PostgreSQL, RabbitMQ UND das Angular-Frontend crm-web laufen automatisch mit)
dotnet run --project AppHost/Crm.AppHost
```

Details zur Aspire-Orchestrierung, den einzelnen Ressourcen und zum manuellen Einzelstart jedes
Service: [`AppHost/README.md`](AppHost/README.md).

## Tests

- Backend-Unit-/Integrationstests (TUnit) liegen unter `Tests/<Service>.Tests/` (z.B.
  `dotnet test Crm.slnx`).
- End-to-End-Tests (Playwright, gegen den echten Stack) inkl. Setup, Seed-Nutzern und
  Testabdeckung je Change-Spec: [`Tests/E2E/README.md`](Tests/E2E/README.md).

## API-Dokumentation (Swagger/OpenAPI)

Jeder Backend-Service stellt im `Development`-Modus unter `/swagger` eine interaktive
OpenAPI-Dokumentation bereit (Swashbuckle), inklusive Request-/Response-Modellen und dem
JWT-Bearer-Schema für `[Authorize]`-geschützte Endpoints:

- `identity-service`: `http://localhost:5179/swagger`
- `customer-service`: `http://localhost:5149/swagger`
- `employee-service`: `http://localhost:5159/swagger`
- `appointment-service`: `http://localhost:5169/swagger`
- `catalog-service`: `http://localhost:5189/swagger`

Die genauen Endpunkte, Rollen und Modelle sind je Capability auch in Abschnitt 5 der zugehörigen
OpenSpec-Datei (siehe oben) beschrieben.

## Projektstruktur (Auszug)

```text
.
├── AppHost/                 # .NET Aspire AppHost + ServiceDefaults
├── Gateway/Crm.Gateway/      # YARP-Reverse-Proxy
├── Services/
│   ├── identity-service/
│   ├── customer-service/
│   ├── employee-service/
│   ├── appointment-service/
│   └── catalog-service/
├── Clients/crm-web/          # Konsolidierte Angular-22-Anwendung
├── Tests/                    # Backend-Unit-/Integrationstests + E2E (Playwright)
├── UserStories/               # OpenSpec Change-Specs & Capability-Specs
├── docs/adr/                  # Architecture Decision Records
└── .claude/                    # Agenten-/Skill-Definitionen (siehe CLAUDE.md)
```

Projektregeln, Tech-Stack und Arbeitsweise des Agenten-Teams: [`CLAUDE.md`](CLAUDE.md).
