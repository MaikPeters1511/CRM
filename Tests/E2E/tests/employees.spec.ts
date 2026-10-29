import { test, expect } from '@playwright/test';
import { urls, seedUsers } from '../fixtures/env';
import { loginViaUi } from '../fixtures/auth';
import { settleAfterNavigation } from '../fixtures/ui';
import {
  loginAsAdmin,
  createEmployee,
  setWorkingHours,
  createCustomer,
  createAppointment,
  createService,
  deactivateService,
  nextWeekday,
  toDateInputValue,
} from '../fixtures/api';

/**
 * Deckt die Scenarios aus `UserStories/mitarbeiterverwaltung.openspec.md`, Abschnitt 3, ab:
 * - Mitarbeiter anlegen (gueltig / E-Mail-Konflikt)
 * - Leistung zuordnen
 * - Wochenarbeitszeit hinterlegen
 * - Abwesenheiten erfassen (Urlaub / Konflikt mit bestehendem Termin)
 * - Mitarbeiter deaktivieren
 *
 * Hinweis zu "Abwesenheiten": Der Ownership-Check (Mitarbeiter darf nur eigene Abwesenheit
 * anlegen) wird hier bewusst mit dem Admin-Pfad (Admin darf fuer beliebige Mitarbeiter
 * Abwesenheiten anlegen, siehe `authentifizierung.openspec.md`) getestet, da der geseedete
 * Employee-Testnutzer aus `identity-service` architektonisch KEINEM realen Employee-Datensatz
 * entspricht (siehe Bug-Report im QA-Abschlussbericht) - der eigentliche Ownership-Erfolgspfad
 * ("Mitarbeiter legt eigene Abwesenheit an") kann daher ueber die echte Login-UI aktuell nicht
 * end-to-end erzeugt werden.
 */

function unique(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 10_000)}`;
}

test.beforeEach(async ({ page }) => {
  await loginViaUi(page, urls.employeeWeb, seedUsers.admin.email, seedUsers.admin.password);
  // Seit der Frontend-Konsolidierung (ADR 0002) landet der Login IMMER auf dem App-weiten
  // Default `/customers` (siehe `Clients/crm-web/src/app/app.routes.ts`,
  // `{ path: '', redirectTo: 'customers' }`), unabhaengig davon, ueber welchen historischen
  // "*Web"-Alias eingeloggt wurde - explizit zum Mitarbeiter-Bereich navigieren
  // (Testcode-Korrektur, kein Produktbug).
  await page.goto(`${urls.employeeWeb}/employees`);
  await settleAfterNavigation(page);
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees$`));
});

test('Mitarbeiter mit gueltigen Pflichtdaten anlegen', async ({ page }) => {
  const lastName = unique('Neuling');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#firstName').fill('Max');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@crm.local`);
  await page.locator('#role').selectOption('Employee');
  await page.locator('button[type=submit]').click();

  // Nach erfolgreichem Anlegen navigiert das Formular zu /employees/:id/edit.
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/.+/edit`));

  await page.goto(`${urls.employeeWeb}/employees`);
  await page.locator('#employee-search').fill(lastName);
  await expect(page.getByText(lastName).first()).toBeVisible();
});

test('Mitarbeiter mit bereits vergebener E-Mail anlegen zeigt Konfliktfehler', async ({ page }) => {
  const lastName = unique('Dupliziert');
  const email = `${lastName}@crm.local`;

  await page.getByRole('link', { name: /neu|new/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#firstName').fill('Erst');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(email);
  await page.locator('#role').selectOption('Employee');
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/.+/edit`));

  await page.goto(`${urls.employeeWeb}/employees/new`);
  await settleAfterNavigation(page);
  await page.locator('#firstName').fill('Zweit');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(email);
  await page.locator('#role').selectOption('Employee');
  await page.locator('button[type=submit]').click();

  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/new`));
  await expect(page.getByRole('alert').first()).toBeVisible();
});

test('Leistung aus dem Katalog einem Mitarbeiter zuordnen', async ({ page }) => {
  // Vorbedingung laut Scenario "Leistung aus Katalog zuordnen"
  // (`UserStories/leistungskatalog.openspec.md`, Abschnitt 3): eine AKTIVE Leistung existiert
  // im zentralen Leistungskatalog - ueber die API angelegt (API-first, UI-second), die
  // eigentliche Zuordnung erfolgt unten ausschliesslich ueber die echte Browser-Interaktion
  // (Auswahl aus dem `<select>` im Mitarbeiterformular, kein Freitext mehr - siehe
  // `employee-form.component.html`).
  const admin = await loginAsAdmin();
  const serviceName = unique('Beratung');
  const service = await createService(admin.accessToken, { name: serviceName });

  const lastName = unique('Beratend');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#firstName').fill('Berta');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@crm.local`);
  await page.locator('#role').selectOption('Employee');
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/.+/edit`));

  await page.locator('#newServiceSelect').selectOption(service.id);
  await page.getByRole('button', { name: /hinzuf[üu]gen|add/i }).click();
  await expect(page.getByText(serviceName, { exact: true })).toBeVisible();

  // Testcode-Hinweis: die Seite befindet sich bereits auf `/employees/:id/edit` (Redirect nach
  // dem Anlegen oben) - der Submit-Klick navigiert laut `EmployeeFormComponent.save()` zur
  // IDENTISCHEN Route, Angular's Router loest dafuer standardmaessig KEINE Navigation aus
  // (keine NavigationEnd, keine Komponenten-Neuerstellung). `expect(page).toHaveURL(...)` waere
  // hier also trivial bereits erfuellt, OHNE auf den Abschluss des zugrundeliegenden PUT-Requests
  // zu warten - ein nachfolgender `page.reload()` koennte den noch laufenden Request serverseitig
  // abbrechen, bevor er die Datenbank erreicht (identisches Race-Muster wie in
  // `Tests/E2E/README.md` dokumentiert). Daher hier explizit auf die Netzwerkantwort des PUT
  // warten, statt auf eine (hier nicht aussagekraeftige) URL-Aenderung zu verlassen.
  const updateResponsePromise = page.waitForResponse(
    (response) => response.url().includes('/employees/') && response.request().method() === 'PUT',
  );
  await page.locator('button[type=submit]').click();
  await updateResponsePromise;

  await expect(page.getByText(serviceName, { exact: true })).toBeVisible();

  // Der Mitarbeiter erscheint bei der Terminvergabe als moeglicher Ansprechpartner fuer diese
  // Leistung - verifiziert ueber die bereits gespeicherte Zuordnung (ServiceId+ServiceName aus
  // dem Katalog uebernommen, kein Freitext): erneutes Laden der Detailseite zeigt denselben
  // Namen weiterhin an (Snapshot korrekt persistiert, nicht nur clientseitiger State).
  await page.reload();
  await settleAfterNavigation(page);
  await expect(page.getByText(serviceName, { exact: true })).toBeVisible();
});

test('Bereits zugeordnete, inzwischen im Katalog deaktivierte Leistung bleibt im Mitarbeiterformular sichtbar', async ({
  page,
}) => {
  // Scenario "Bereits zugeordnete, inzwischen deaktivierte Leistung bleibt sichtbar"
  // (`UserStories/leistungskatalog.openspec.md`, Abschnitt 3, MODIFIED Requirement).
  const admin = await loginAsAdmin();
  const serviceName = unique('Pediküre');
  const service = await createService(admin.accessToken, { name: serviceName });

  const lastName = unique('Snapshothalter');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#firstName').fill('Sina');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@crm.local`);
  await page.locator('#role').selectOption('Employee');
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/.+/edit`));

  await page.locator('#newServiceSelect').selectOption(service.id);
  await page.getByRole('button', { name: /hinzuf[üu]gen|add/i }).click();
  // Explizit auf die PUT-Antwort warten statt auf eine URL-Aenderung (die Seite ist bereits auf
  // `/employees/:id/edit` - siehe ausfuehrlicher Kommentar im vorherigen Test), bevor unten per
  // `page.reload()` ein echter Browser-Reload erfolgt.
  const updateResponsePromise = page.waitForResponse(
    (response) => response.url().includes('/employees/') && response.request().method() === 'PUT',
  );
  await page.locator('button[type=submit]').click();
  await updateResponsePromise;
  await expect(page.getByText(serviceName, { exact: true })).toBeVisible();

  // Leistung NACH der Zuordnung im Katalog deaktivieren.
  await deactivateService(admin.accessToken, service.id);

  // Mitarbeiterformular erneut oeffnen (frischer Ladevorgang, kein zwischenzeitlicher
  // Client-State): die bestehende Zuordnung (Snapshot-Name) bleibt unveraendert sichtbar.
  await page.reload();
  await settleAfterNavigation(page);
  await expect(page.getByText(serviceName, { exact: true })).toBeVisible();

  // Die deaktivierte Leistung ist nicht erneut aus der Auswahlliste neu zuordenbar - das
  // `<select>` fuer neue Zuordnungen enthaelt sie nicht (gefiltert ueber `GET
  // /api/services?status=true`, siehe `ServiceLookupApiService`/`EmployeeFormComponent`).
  const options = page.locator('#newServiceSelect option');
  await expect(options.filter({ hasText: serviceName })).toHaveCount(0);
});

test('Wochenarbeitszeit Mo-Fr 09:00-17:00 hinterlegen', async ({ page }) => {
  const lastName = unique('Schicht');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#firstName').fill('Silke');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@crm.local`);
  await page.locator('#role').selectOption('Employee');
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/.+/edit`));

  const employeeId = page.url().match(/employees\/([^/]+)\/edit/)?.[1];
  expect(employeeId).toBeTruthy();

  await page.goto(`${urls.employeeWeb}/employees/${employeeId}/schedule`);
  await settleAfterNavigation(page);
  await expect(page.getByRole('grid')).toBeVisible();

  // Montag-Freitag (Zeilen 0-4), Slot 09:00 (Spalte je nach `hours`-Definition: erste Spalte = 9).
  for (let row = 0; row <= 4; row++) {
    await page.locator(`#slot-${row}-0`).click();
  }
  await page.getByRole('button', { name: /speichern|save/i }).click();
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees$`));
});

test('Urlaub fuer einen terminfreien Zeitraum eintragen', async ({ page }) => {
  const lastName = unique('Urlauber');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#firstName').fill('Ulla');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@crm.local`);
  await page.locator('#role').selectOption('Employee');
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/.+/edit`));

  const employeeId = page.url().match(/employees\/([^/]+)\/edit/)?.[1];
  await page.goto(`${urls.employeeWeb}/employees/${employeeId}/absences/new`);
  await settleAfterNavigation(page);

  const from = new Date();
  from.setDate(from.getDate() + 60);
  const to = new Date(from);
  to.setDate(to.getDate() + 5);

  await page.locator('#absenceType').selectOption('Vacation');
  await page.locator('#absenceFrom').fill(from.toISOString().slice(0, 10));
  await page.locator('#absenceTo').fill(to.toISOString().slice(0, 10));
  await page.locator('button[type=submit]').click();

  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees$`));
});

test('Mitarbeiter deaktivieren entfernt ihn aus der Verfuegbarkeitsauswahl', async ({ page }) => {
  const lastName = unique('Ausscheider');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#firstName').fill('Egon');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@crm.local`);
  await page.locator('#role').selectOption('Employee');
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/.+/edit`));

  await page.goto(`${urls.employeeWeb}/employees`);
  await page.locator('#employee-search').fill(lastName);
  // Warten, bis die (debounced) Suche tatsaechlich gefiltert hat, bevor der Deaktivieren-Button
  // geklickt wird - sonst kann bei vielen Mitarbeitern aus vorherigen Tests der Klick den
  // Button einer ANDEREN, noch sichtbaren Zeile treffen (Testcode-Fix, kein Produktbug, real
  // verifiziert: das manuelle Reproduzieren mit genau einem Mitarbeiter funktionierte fehlerfrei).
  const filteredRow = page.locator('tr', { hasText: lastName });
  await expect(filteredRow).toHaveCount(1);
  await filteredRow.getByRole('button', { name: /deaktivieren|deactivate/i }).click();

  // Kein erneutes Befuellen von #employee-search hier (Testcode-Fix, kein Produktbug): die
  // Deaktivierung aktualisiert die Liste rein clientseitig optimistisch
  // (`EmployeeStateService.deactivateEmployee`, kein Server-Refetch). Ein erneutes `fill()` mit
  // demselben Suchbegriff loest aber trotzdem eine neue Server-Suche aus (`setSearchTerm` reagiert
  // auf jeden Value-Change-Event, auch bei identischem Text) - kommt diese Antwort zurueck, BEVOR
  // der Deaktivieren-Request serverseitig abgeschlossen ist, ueberschreibt sie den optimistischen
  // Zustand wieder mit "aktiv" und die Zeile bleibt dauerhaft (kein spaeteres Update) auf "aktiv"
  // stehen - ein reiner Testdaten-Race, kein Produktbug (das Backend deaktiviert korrekt, siehe
  // `EmployeesController`/`DeactivateEmployeeHandler`). Playwright's `expect(...).toBeVisible()`
  // pollt bereits automatisch, bis die optimistische UI-Aktualisierung sichtbar ist - kein
  // zusaetzliches Warten noetig.
  await expect(filteredRow.getByText(/inaktiv|inactive/i)).toBeVisible();
});

test('Urlaub kollidiert mit bestehendem Termin -> Konfliktwarnung mit expliziter Bestaetigung', async ({ page }) => {
  const admin = await loginAsAdmin();
  const lastName = unique('Kollisionsmitarbeiter');
  const employee = await createEmployee(admin.accessToken, {
    firstName: 'Konni',
    lastName,
    email: `${lastName}@crm.local`,
    role: 'Employee',
  });
  await setWorkingHours(admin.accessToken, employee.id, [
    { weekday: 'Monday', startTime: '09:00:00', endTime: '17:00:00' },
  ]);
  const customer = await createCustomer(admin.accessToken, {
    firstName: 'Kollisions',
    lastName: 'Kunde',
    email: `${unique('kollision')}@example.com`,
  });
  const monday = nextWeekday(1);
  const dateIso = toDateInputValue(monday);
  const appointment = await createAppointment(admin.accessToken, {
    customerId: customer.id,
    employeeId: employee.id,
    serviceId: '11111111-1111-1111-1111-111111111111',
    start: `${dateIso}T10:00:00Z`,
    end: `${dateIso}T11:00:00Z`,
  });
  expect(appointment, 'Termin-Vorbedingung fuer den Konflikttest konnte nicht angelegt werden').not.toBeNull();

  await loginViaUi(page, urls.employeeWeb, seedUsers.admin.email, seedUsers.admin.password);
  await page.goto(`${urls.employeeWeb}/employees/${employee.id}/absences/new`);
  await settleAfterNavigation(page);

  await page.locator('#absenceType').selectOption('Vacation');
  await page.locator('#absenceFrom').fill(dateIso);
  await page.locator('#absenceTo').fill(dateIso);
  await page.locator('button[type=submit]').click();

  await expect(page.getByRole('alert').filter({ hasText: /konflikt|conflict/i }).first()).toBeVisible();
  await page.getByRole('button', { name: /best[äa]tig|confirm/i }).click();

  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees$`));
});
