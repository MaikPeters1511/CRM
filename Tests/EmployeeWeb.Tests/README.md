# EmployeeWeb.Tests

Unit-Tests fuer das Angular-Frontend `Clients/employee-web` (Mitarbeiterverwaltung).
Gemaess CLAUDE.md liegen alle Tests zentral im Ordner `Tests/`, auch wenn der
Angular-Quellcode selbst in `Clients/employee-web/src` liegt.

## Struktur
- `application/` – Tests fuer den State-Service (`EmployeeStateService`), inkl.
  Fehler-Rollback und Abwesenheits-Konfliktbehandlung (`ConfirmOverride`).
- `infrastructure/` – Tests fuer den HTTP-API-Client (`EmployeeApiService`).
- `presentation/` – Komponenten-Tests (`EmployeeListComponent`, `EmployeeFormComponent`,
  `EmployeeScheduleComponent`, `AbsenceFormComponent`).

Jede Testdatei importiert die Produktionsquellen per relativem Pfad aus
`../../../Clients/employee-web/src/app/...`.

## Ausfuehren
```bash
cd Clients/employee-web
npm test        # entspricht: ng test --watch=false
```

`angular.json` (`projects.employee-web.architect.test.options.include`) verweist
per **absolutem** Pfad auf dieses Verzeichnis, weil der Vitest-Test-Discovery
(`tinyglobby`) keine `../`-Traversierung aus dem Projekt-Source-Root heraus
unterstuetzt (identische Notloesung wie in `Tests/CustomerWeb.Tests/README.md`).
Beim Klonen auf eine andere Maschine muss dieser Pfad in `angular.json`
angepasst werden, falls das Repository nicht unter `/Users/maik/Desktop/projekte/crm`
liegt.

## Modulaufloesung
Da die Testdateien ausserhalb des Angular-Projektordners liegen, benoetigt
Node/Vite fuer die Aufloesung von `node_modules`-Paketen (z. B. `@angular/core`,
`rxjs`) einen Pfad, der von hier aus erreichbar ist. Es wird derselbe
Repo-Root-Symlink `crm/node_modules -> Clients/customer-web/node_modules`
wiederverwendet (siehe `Tests/CustomerWeb.Tests/README.md`); `employee-web` hat
dieselben Abhaengigkeitsversionen wie `customer-web` (Angular 22, Vitest 4).
