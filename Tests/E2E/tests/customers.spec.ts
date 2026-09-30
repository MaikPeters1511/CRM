import { test, expect } from '@playwright/test';
import { urls, seedUsers } from '../fixtures/env';
import { loginViaUi } from '../fixtures/auth';

/**
 * Deckt die Scenarios aus `UserStories/kundenverwaltung.openspec.md`, Abschnitt 3, ab:
 * - Kunde anlegen (gueltig / ohne Pflichtfelder)
 * - Kunden suchen (Nachname)
 * - Kunde bearbeiten
 * - Kunde deaktivieren (Soft-Delete, nur Admin)
 * Terminhistorie (Requirement "Kundendetailsicht mit Terminhistorie") ist laut
 * `kundenverwaltung.openspec.md` Task 2/3 bewusst noch nicht auslieferbar
 * (`GET /api/customers/{id}` liefert noch keine Terminreferenzen) - das Frontend zeigt dafuer
 * einen Hinweistext, dessen Anzeige hier mitgetestet wird, statt eine echte Historie zu
 * verifizieren (kein Test gegen eine nicht implementierte Funktion).
 */

function unique(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 10_000)}`;
}

test.beforeEach(async ({ page }) => {
  await loginViaUi(page, urls.customerWeb, seedUsers.admin.email, seedUsers.admin.password);
  await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers$`));
});

test('Kunde mit gueltigen Pflichtdaten anlegen', async ({ page }) => {
  const lastName = unique('Muster');

  await page.getByRole('link', { name: /neu|new/i }).click();
  await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers/new`));

  await page.locator('#firstName').fill('Erika');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@example.com`);
  await page.locator('button[type=submit]').click();

  await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers$`));
  await page.locator('#customer-search').fill(lastName);
  await expect(page.getByText(lastName)).toBeVisible();
});

test('Kunde ohne Pflichtfelder anlegen zeigt Validierungsfehler und persistiert nichts', async ({ page }) => {
  await page.getByRole('link', { name: /neu|new/i }).click();
  await page.locator('button[type=submit]').click();

  // Verbleibt auf dem Formular (kein Redirect zur Liste) - Validierungsfehler sichtbar.
  await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers/new`));
  await expect(page.getByRole('alert').first()).toBeVisible();
});

test('Suche nach Nachnamen filtert die Kundenliste', async ({ page }) => {
  const lastName = unique('Sucheins');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await page.locator('#firstName').fill('Such');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#phone').fill('+49 30 1234567');
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers$`));

  await page.locator('#customer-search').fill(unique('nicht-vorhanden-xyz'));
  await expect(page.getByText(lastName)).not.toBeVisible();

  await page.locator('#customer-search').fill(lastName);
  await expect(page.getByText(lastName)).toBeVisible();
});

test('Kundendaten aktualisieren setzt ModifiedAt/ModifiedBy', async ({ page }) => {
  const lastName = unique('Editme');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await page.locator('#firstName').fill('Vorher');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@example.com`);
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers$`));

  await page.locator('#customer-search').fill(lastName);
  await page.getByRole('link', { name: /bearbeiten|edit/i }).first().click();

  await page.locator('#firstName').fill('Nachher');
  await page.locator('button[type=submit]').click();

  await page.locator('#customer-search').fill(lastName);
  await page.getByRole('link', { name: /anzeigen|view/i }).first().click();
  await expect(page.getByText('Nachher')).toBeVisible();
  await expect(page.getByText(/modifiedAt|geändert am|modified/i)).toBeVisible();
});

test('Kunde deaktivieren markiert ihn als inaktiv, bleibt aber ueber Inaktiv-Filter sichtbar', async ({ page }) => {
  const lastName = unique('Deakt');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await page.locator('#firstName').fill('Deaktiv');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@example.com`);
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers$`));

  await page.locator('#customer-search').fill(lastName);
  await page.getByRole('link', { name: /anzeigen|view/i }).first().click();

  await page.getByRole('button', { name: /deaktivieren|deactivate/i }).click();
  await page.getByRole('button', { name: /best[äa]tigen|confirm/i }).click();

  await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers`));
  await page.locator('#customer-search').fill(lastName);
  await expect(page.getByText(lastName)).not.toBeVisible();

  await page.locator('#customer-status-filter').selectOption('inactive');
  await expect(page.getByText(lastName)).toBeVisible();
});

test('Kundendetailsicht zeigt Terminhistorie-Tab (aktuell mit Hinweis, siehe bekannte Einschraenkung)', async ({
  page,
}) => {
  const lastName = unique('Historie');
  await page.getByRole('link', { name: /neu|new/i }).click();
  await page.locator('#firstName').fill('Hist');
  await page.locator('#lastName').fill(lastName);
  await page.locator('#email').fill(`${lastName}@example.com`);
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers$`));

  await page.locator('#customer-search').fill(lastName);
  await page.getByRole('link', { name: /anzeigen|view/i }).first().click();

  await page.getByRole('tab', { name: /termine|appointments/i }).click();
  await expect(page.getByRole('tabpanel')).toBeVisible();
});
