# catalog-service

Leistungskatalog (Bounded Context "Service Catalog"). Fachliche Spezifikation:
[`UserStories/leistungskatalog.openspec.md`](../../UserStories/leistungskatalog.openspec.md).
Architektur: [`docs/adr/0001-microservices-architektur-crm.md`](../../docs/adr/0001-microservices-architektur-crm.md).

## Starten

```bash
cd Services/catalog-service/src/CatalogService.Web
dotnet run --launch-profile http
```

Port (Dev, Direct): `http://localhost:5189`. Swagger/OpenAPI (nur `Development`):
`http://localhost:5189/swagger`. Üblicherweise wird der Service stattdessen über den
[AppHost](../../AppHost/README.md) mitgestartet.

## Endpoints (Übersicht, Details siehe Swagger/OpenSpec Abschnitt 5)

- `GET /api/services?search=&status=&page=` — Paginierte, filterbare Leistungsliste (Rolle `Employee`, `Admin`)
- `GET /api/services/{id}` — Leistungsdetails (Rolle `Employee`, `Admin`)
- `POST /api/services` — Anlegen (Rolle `Admin`)
- `PUT /api/services/{id}` — Bearbeiten (Rolle `Admin`)
- `POST /api/services/{id}/deactivate` — Soft-Delete (Rolle `Admin`)

## Datenbank

Eigene PostgreSQL-Instanz `catalog_db` (Database-per-Service, siehe ADR 0001). Migrationen
werden beim Start automatisch angewendet (`DatabaseMigrationHostedService`).

## Abhängigkeiten

- Nur `crm-web` (Frontend) ruft `catalog-service` direkt über das Gateway auf, um die Leistungsauswahl
  im Mitarbeiterformular zu befüllen. Es gibt keinen synchronen Backend-zu-Backend-Call von
  `employee-service`/`appointment-service` zu `catalog-service` — `EmployeeServiceAssignment`
  speichert `ServiceId`/`ServiceName` als Snapshot-Werte zum Zeitpunkt der Zuordnung.
- Kein Integration-Event-Publishing (kein Konsument vorhanden, siehe OpenSpec Abschnitt 7 — kein
  Gold-Plating).
- JWT-Validierung gegen `identity-service` (gemeinsamer Signing-Key, kein OIDC-Discovery).
