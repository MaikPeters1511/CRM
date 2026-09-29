# CustomerWeb.Tests

Unit-Tests fuer das Angular-Frontend `Clients/customer-web` (Kundenverwaltung).
Gemaess CLAUDE.md liegen alle Tests zentral im Ordner `Tests/`, auch wenn der
Angular-Quellcode selbst in `Clients/customer-web/src` liegt.

## Struktur
- `application/` – Tests fuer den State-Service (`CustomerStateService`), inkl. Fehler-Rollback.
- `infrastructure/` – Tests fuer den HTTP-API-Client (`CustomerApiService`).
- `presentation/` – Komponenten-Tests (`CustomerListComponent`, `CustomerFormComponent`, `CustomerDetailComponent`).

Jede Testdatei importiert die Produktionsquellen per relativem Pfad aus
`../../../Clients/customer-web/src/app/...`.

## Ausfuehren
```bash
cd Clients/customer-web
npm test        # entspricht: ng test --watch=false
```

`angular.json` (`projects.customer-web.architect.test.options.include`) verweist
per **absolutem** Pfad auf dieses Verzeichnis, weil der Vitest-Test-Discovery
(`tinyglobby`) keine `../`-Traversierung aus dem Projekt-Source-Root heraus
unterstuetzt. Beim Klonen auf eine andere Maschine muss dieser Pfad in
`angular.json` angepasst werden, falls das Repository nicht unter
`/Users/maik/Desktop/projekte/crm` liegt.

## Modulaufloesung
Da die Testdateien ausserhalb des Angular-Projektordners liegen, benoetigt
Node/Vite fuer die Aufloesung von `node_modules`-Paketen (z. B. `@angular/core`,
`rxjs`) einen Pfad, der von hier aus erreichbar ist. Dafuer existiert am
Repo-Root ein Symlink `crm/node_modules -> Clients/customer-web/node_modules`.
Falls dieser Symlink fehlt (z. B. nach einem frischen Checkout), erzeugen mit:

```bash
ln -s Clients/customer-web/node_modules node_modules
```
