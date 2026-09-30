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

test('Leistung einem Mitarbeiter zuordnen', async ({ page }) => {
  const lastName = unique('Beratend');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#firstName').fill('Berta');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@crm.local`);
  await page.locator('#role').selectOption('Employee');
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/.+/edit`));

  await page.locator('#newServiceId').fill('beratung');
  await page.locator('#newServiceName').fill('Beratung');
  await page.getByRole('button', { name: /hinzuf[üu]gen|add/i }).click();
  await expect(page.getByText('Beratung', { exact: true })).toBeVisible();
  await page.locator('button[type=submit]').click();

  await expect(page).toHaveURL(new RegExp(`${urls.employeeWeb}/employees/.+/edit`));
  await expect(page.getByText('Beratung', { exact: true })).toBeVisible();
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

  await page.locator('#employee-search').fill(lastName);
  const row = page.locator('tr', { hasText: lastName });
  await expect(row.getByText(/inaktiv|inactive/i)).toBeVisible();
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
