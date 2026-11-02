# Change: MP CRM Mockup-Redesign (Shell, Dashboard, Restyling)

> **Status:** proposed → apply (diese Session setzt direkt um; Nutzer hat das Mockup im Chat bereits final abgenommen)

## Proposal
Umsetzung des mit dem Nutzer im Chat erarbeiteten Klick-Mockups für `Clients/crm-web`:
neues DaisyUI-Theme „mpcrm" (teal/ivory), Sidebar-Shell statt Top-Navbar, neuer Feature-Bereich
`dashboard/`, visuelles Restyling der bestehenden Bereiche (`customers`, `employees`,
`appointments`, `catalog`) und des KI-Assistent-Chat-Widgets. Produktname wechselt von „CRM" zu
„MP CRM". Kein neues Backend, keine neuen Endpunkte — das Dashboard aggregiert ausschließlich
bereits vorhandene Daten (`GET /api/appointments`, Kunden-/Mitarbeiter-Suche).

Explizit außerhalb des Scopes: Telefonie/3CX, KI-Einstellungen, Abwesenheits-Freigabe-Workflow,
Theme-Editor, Audit-Log, Zahlungsanbieter-/DATEV-/SMTP-/Kalender-Sync-Integrationen, MFA/Passkeys,
Einstellungen-Seiten generell — diese Mockup-Screens bleiben unangetastet bzw. werden nicht gebaut.

## Design
- **Theme:** Eigenes DaisyUI-5-Theme `mpcrm` (`src/styles.css`), `base-100` weiß (Kartenfläche),
  `base-200` Ivory `#F6F4EF` (Seitenhintergrund), `base-300` `#E6E2D8` (Rahmen/Elevation),
  `primary`/`neutral` Teal `#0F6B63` bzw. `#0B4F49`.
- **Shell:** `app.html`/`app.ts` von Top-Navbar auf linke Sidebar umgestellt (Logo „MP" +
  Produktname, Navigationsliste, Nutzer-Karte unten). Die bestehende rollenbasierte Sichtbarkeit
  (`authState.isAdmin()` für `catalog`) und das bestehende `nav[aria-label="crm.nav.main"]`
  bleiben erhalten, jetzt ergänzt um `dashboard` und `assistant`-Platzhalter-Link.
- **Dashboard (`dashboard/`):** Neuer Feature-Bereich nach demselben
  `domain|application|infrastructure|presentation`-Schema wie die bestehenden Bereiche. Liest
  **nur** über bereits vorhandene `AppointmentApiService`/`CustomerApiService`/`EmployeeApiService`
  (keine neuen Backend-Endpunkte). Kennzahl-Kacheln: Termine heute, offene Anfragen
  (`RequiresReview`), aktive Kunden, Mitarbeiter gesamt — jeweils aus vorhandenen Listen-/
  Such-Antworten abgeleitet. "Neue Kunden diesen Monat" und "Team-Auslastung" werden bewusst
  **nicht** umgesetzt, da ohne neuen Backend-Endpunkt nicht sauber ableitbar (keine erfundenen
  Zahlen). Liste „Anstehende Termine" nutzt `GET /api/appointments` (rollenbasiert bereits durch
  Backend eingeschränkt, siehe `appointment-scheduling.openspec.md` Abschnitt "RBAC"). KI-Vorschau-
  Karte verlinkt auf das bestehende `assistant-chat-widget`.
- **Restyling:** Bestehende Templates (`customer-list`, `employee-list`, `calendar-view`,
  `catalog-list` usw.) bekommen Karten-/Badge-/Tabellen-Klassen des neuen Themes; keine
  Änderung an Komponentenlogik, State-Services oder bestehenden Tests außer wo die Shell-/Nav-
  Struktur selbst betroffen ist (`shell/app.spec.ts`).

## Tasks
- [x] DaisyUI-Theme `mpcrm` definieren
- [x] Sidebar-Shell (`app.html`/`app.ts`/`app.routes.ts`, `/dashboard` als neue Default-Route)
- [x] `dashboard/`-Feature-Bereich inkl. Tests (TDD)
- [x] Restyling `customers`/`employees`/`appointments`/`catalog`
- [x] Restyling `assistant-chat-widget`/`assistant-suggestion-card`
- [x] i18n-Keys (DE/EN) ergänzen, `crm.nav.title` → „MP CRM"
- [x] Build + bestehende/neue Tests grün
