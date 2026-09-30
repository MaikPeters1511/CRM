# CrmWeb.Tests

Unit-Tests fuer das konsolidierte Angular-Frontend `Clients/crm-web` (siehe
`docs/adr/0002-frontend-konsolidierung.md` und
`UserStories/frontend-konsolidierung.openspec.md`). Ersetzt die vormals getrennten
Testordner `Tests/CustomerWeb.Tests`, `Tests/EmployeeWeb.Tests`, `Tests/AppointmentWeb.Tests`
(entfernt nach erfolgreicher Migration).

## Struktur
Gespiegelt an `Clients/crm-web/src/app/`:
- `auth/` – gemeinsamer Auth-Flow (`AuthStateService`, `authGuard`), einmalig aus den drei
  (identischen) bisherigen Implementierungen uebernommen.
- `customers/` – Kunden-Feature-Bereich (migriert aus `Tests/CustomerWeb.Tests`).
- `employees/` – Mitarbeiter-Feature-Bereich (migriert aus `Tests/EmployeeWeb.Tests`).
- `appointments/` – Termin-Feature-Bereich (migriert aus `Tests/AppointmentWeb.Tests`).
- `shell/` – neue Tests fuer die gemeinsame Shell/Navigation
  (`UserStories/frontend-konsolidierung.openspec.md` Abschnitt 3: "Einheitliche
  Frontend-Anwendung" / "Gemeinsame Navigation"): Wechsel zwischen Modulen ohne
  erneuten Login, rollenbasierte Anzeige der Navigation.

Jede Testdatei importiert die Produktionsquellen per relativem Pfad aus
`../../../../Clients/crm-web/src/app/...` (bzw. `../../../Clients/crm-web/src/app/...`
fuer `shell/`, da dort eine Verzeichnisebene weniger vorhanden ist).

## Ausfuehren
```bash
cd Clients/crm-web
npm test        # entspricht: ng test --watch=false
```

`angular.json` (`projects.crm-web.architect.test.options.include`) verweist per
**absolutem** Pfad auf dieses Verzeichnis, weil der Vitest-Test-Discovery
(`tinyglobby`) keine `../`-Traversierung aus dem Projekt-Source-Root heraus
unterstuetzt (identische Notloesung wie in den vormaligen `*.Web.Tests`-Ordnern).
Beim Klonen auf eine andere Maschine muss dieser Pfad in `angular.json` angepasst
werden, falls das Repository nicht unter `/Users/maik/Desktop/projekte/crm` liegt.

## Modulaufloesung
Da die Testdateien ausserhalb des Angular-Projektordners liegen, benoetigt
Node/Vite fuer die Aufloesung von `node_modules`-Paketen (z. B. `@angular/core`,
`rxjs`) einen Pfad, der von hier aus erreichbar ist. Der Repo-Root-Symlink
`crm/node_modules` zeigt auf `Clients/crm-web/node_modules`.
