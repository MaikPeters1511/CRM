# employee-service

Mitarbeiterverwaltung (Bounded Context "Employee Management"). Fachliche Spezifikation:
[`UserStories/mitarbeiterverwaltung.openspec.md`](../../UserStories/mitarbeiterverwaltung.openspec.md).
Architektur: [`docs/adr/0001-microservices-architektur-crm.md`](../../docs/adr/0001-microservices-architektur-crm.md).

## Starten

```bash
cd Services/employee-service/src/EmployeeService.Web
dotnet run --launch-profile http
```

Port (Dev, Direct): `http://localhost:5159`. Swagger/OpenAPI (nur `Development`):
`http://localhost:5159/swagger`. Üblicherweise wird der Service stattdessen über den
[AppHost](../../AppHost/README.md) mitgestartet.

## Endpoints (Übersicht, Details siehe Swagger/OpenSpec Abschnitt 5)

- `GET /api/employees` — Suche/Filterung nach Rolle/Leistung (Rolle `Employee`, `Admin`)
- `GET /api/employees/{id}` — Details inkl. Leistungen/Arbeitszeiten/Abwesenheiten
- `POST /api/employees` — Anlegen (Rolle `Admin`)
- `PUT /api/employees/{id}` — Bearbeiten (Rolle `Admin`)
- `POST /api/employees/{id}/deactivate` — Soft-Delete (Rolle `Admin`), publiziert `EmployeeDeactivated`
- `PUT /api/employees/{id}/schedule` — Wochenarbeitszeiten setzen (Rolle `Admin`)
- `POST /api/employees/{id}/absences` — Abwesenheit anlegen (Ownership-Check: `Employee` nur eigener Datensatz)
- `GET /api/employees/{id}/availability` — Verfügbare Zeitfenster

## Datenbank

Eigene PostgreSQL-Instanz `employee_db` (Database-per-Service, siehe ADR 0001). Migrationen
werden beim Start automatisch angewendet.

## Abhängigkeiten

- Ruft bei Abwesenheits-/Arbeitszeitkonflikten synchron `appointment-service` ab
  (`IAppointmentAvailabilityClient`, Polly-Resilienz).
- `appointment-service` fragt umgekehrt Verfügbarkeit per `GET /api/employees/{id}/availability` ab.
- Publiziert das Integration Event `EmployeeDeactivated`.
- JWT-Validierung gegen `identity-service` (gemeinsamer Signing-Key, kein OIDC-Discovery).
