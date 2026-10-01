# customer-service

Kundenverwaltung (Bounded Context "Customer Management"). Fachliche Spezifikation:
[`UserStories/kundenverwaltung.openspec.md`](../../UserStories/kundenverwaltung.openspec.md).
Architektur: [`docs/adr/0001-microservices-architektur-crm.md`](../../docs/adr/0001-microservices-architektur-crm.md).

## Starten

```bash
cd Services/customer-service/src/CustomerService.Web
dotnet run --launch-profile http
```

Port (Dev, Direct): `http://localhost:5149`. Swagger/OpenAPI (nur `Development`):
`http://localhost:5149/swagger`. Üblicherweise wird der Service stattdessen über den
[AppHost](../../AppHost/README.md) mitgestartet.

## Endpoints (Übersicht, Details siehe Swagger/OpenSpec Abschnitt 5)

- `GET /api/customers` — Suche/Filterung (Rolle `Employee`, `Admin`)
- `GET /api/customers/{id}` — Details (Rolle `Employee`, `Admin`)
- `POST /api/customers` — Anlegen (Rolle `Employee`, `Admin`)
- `PUT /api/customers/{id}` — Bearbeiten (Rolle `Employee`, `Admin`)
- `POST /api/customers/{id}/deactivate` — Soft-Delete (Rolle `Admin`), publiziert `CustomerDeactivated`

## Datenbank

Eigene PostgreSQL-Instanz `customer_db` (Database-per-Service, siehe ADR 0001). Migrationen
werden beim Start automatisch angewendet (`DatabaseMigrationHostedService`).

## Abhängigkeiten

- `appointment-service` fragt Kundendaten synchron per `GET /api/customers/{id}` ab (Existenz-/
  Aktivstatusprüfung vor Terminanlage).
- Publiziert das Integration Event `CustomerDeactivated` (Wolverine/RabbitMQ) für `appointment-service`
  (Kompensationslogik `RequiresReview`).
- JWT-Validierung gegen `identity-service` (gemeinsamer Signing-Key, kein OIDC-Discovery).
