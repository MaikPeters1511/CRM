# appointment-service

Terminverwaltung (Bounded Context "Appointment Scheduling") inkl. RBAC und Kalenderansicht.
Fachliche Spezifikation: [`UserStories/terminverwaltung.openspec.md`](../../UserStories/terminverwaltung.openspec.md).
Architektur: [`docs/adr/0001-microservices-architektur-crm.md`](../../docs/adr/0001-microservices-architektur-crm.md).

## Starten

```bash
cd Services/appointment-service/src/AppointmentService.Web
dotnet run --launch-profile http
```

Port (Dev, Direct): `http://localhost:5169`. Swagger/OpenAPI (nur `Development`):
`http://localhost:5169/swagger`. Üblicherweise wird der Service stattdessen über den
[AppHost](../../AppHost/README.md) mitgestartet.

## Endpoints (Übersicht, Details siehe Swagger/OpenSpec Abschnitt 5)

- `GET /api/appointments` — Kalenderliste; Rolle `Employee` sieht ausschließlich eigene Termine
  (Ownership-Filter über `EmployeeId`), Rolle `Admin` sieht alle
- `GET /api/appointments/{id}` — Details (Ownership-Check wie oben)
- `POST /api/appointments` — Anlegen inkl. Kollisionsprüfung und Validierung von Kunde/Mitarbeiter
- `PUT /api/appointments/{id}/reschedule` — Verschieben (`403` bei Fremdzugriff ohne `Admin`)
- `POST /api/appointments/{id}/cancel` — Stornieren (`403` bei Fremdzugriff ohne `Admin`)

## Datenbank

Eigene PostgreSQL-Instanz `appointment_db` (Database-per-Service, siehe ADR 0001). Migrationen
werden beim Start automatisch angewendet. Enthält zusätzlich den Erinnerungs-Job (Hosted Service,
Log-Stub-Notifier, kein echter E-Mail-Versand).

## Abhängigkeiten

- Ruft synchron `customer-service` (`GET /api/customers/{id}`) und `employee-service`
  (`GET /api/employees/{id}/availability`) ab, jeweils Polly-abgesichert (Timeout/Retry/
  Circuit-Breaker).
- Konsumiert das Integration Event `CustomerDeactivated` (Kompensationslogik `RequiresReview`).
- Publiziert `AppointmentCreated`/`AppointmentCancelled`/`AppointmentRescheduled`.
- JWT-Validierung gegen `identity-service` (gemeinsamer Signing-Key, kein OIDC-Discovery).
