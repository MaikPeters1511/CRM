# AppointmentWeb.Tests

Unit-Tests fuer das Angular-Frontend `Clients/appointment-web` (Terminverwaltung/Kalender).
Gemaess CLAUDE.md liegen alle Tests zentral im Ordner `Tests/`, auch wenn der
Angular-Quellcode selbst in `Clients/appointment-web/src` liegt.

## Struktur
- `application/` – Tests fuer die State-Services (`AuthStateService`, `CalendarStateService`)
  und die reine Kalender-Range-Utility (`calendar-range.util`).
- `infrastructure/` – Tests fuer die HTTP-API-Clients (`AppointmentApiService`,
  `CustomerApiService` fuer den Kunde-Autocomplete, `EmployeeApiService` fuer
  Mitarbeiter-/Leistungsauswahl und Live-Verfuegbarkeit).
- `presentation/` – Komponenten-Tests (`CalendarViewComponent`, `AppointmentFormComponent`,
  `AppointmentDetailComponent`, `authGuard`).

Jede Testdatei importiert die Produktionsquellen per relativem Pfad aus
`../../../Clients/appointment-web/src/app/...`.

## Ausfuehren
```bash
cd Clients/appointment-web
npm test        # entspricht: ng test --watch=false
```

`angular.json` (`projects.appointment-web.architect.test.options.include`) verweist
per **absolutem** Pfad auf dieses Verzeichnis (gleiche Notloesung wie bei
`customer-web`/`employee-web`, siehe `Tests/CustomerWeb.Tests/README.md`).

## node_modules (wichtig, abweichend von customer-web/employee-web)
`Clients/appointment-web/node_modules` ist bewusst ein **Symlink** auf
`../customer-web/node_modules` (nicht ein eigenstaendig installierter Ordner):

```bash
ln -s ../customer-web/node_modules node_modules
```

Grund: Eine frische `npm install` direkt in `appointment-web` erzeugt eine leicht
abweichende Aufloesung transitiver Pakete (u.a. `vite`/`lightningcss`-Unterbaum).
In dieser Konstellation initialisiert Angular's Vitest-Testumgebung
(`getTestBed().initTestEnvironment(...)`, generiert vom `@angular/build:unit-test`-Builder)
beim allerersten `TestBed.configureTestingModule()`-Aufruf nicht zuverlaessig
(`TypeError: Cannot read properties of null (reading 'ngModule')` bzw. anschliessend
`Need to call TestBed.initTestEnvironment() first` fuer alle weiteren Tests im selben
Prozess). Mit dem Symlink auf den bereits funktionierenden `customer-web`-Baum
(identische `package.json`-Versionen) tritt der Fehler nicht auf. Falls dieser Symlink
fehlt oder auf eine andere Maschine geklont wird, zuerst `customer-web`/`employee-web`
installieren und dann den Symlink neu anlegen, statt `npm install` direkt in
`appointment-web` auszufuehren.
