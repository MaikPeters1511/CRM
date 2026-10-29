import { test, expect } from '@playwright/test';
import { urls, seedUsers } from '../fixtures/env';
import { loginViaUi } from '../fixtures/auth';
import { settleAfterNavigation } from '../fixtures/ui';

/**
 * Deckt die Scenarios aus `UserStories/leistungskatalog.openspec.md`, Abschnitt 3, ab:
 * - Leistung anlegen (gueltig / ohne Namen)
 * - Leistungen suchen und auflisten (Suche nach Namen)
 * - Leistung bearbeiten
 * - Leistung deaktivieren (Soft-Delete, inkl. Sichtbarkeit ueber Status-Filter)
 *
 * Das MODIFIED Requirement "Leistungen/Qualifikationen zuordnen" (Scenario "Leistung aus Katalog
 * zuordnen" / "Bereits zugeordnete, inzwischen deaktivierte Leistung bleibt sichtbar") wird in
 * `employees.spec.ts` abgedeckt, da es inhaltlich zum Mitarbeiterformular gehoert.
 *
 * Der Leistungskatalog ist laut `UserStories/leistungskatalog.openspec.md` Abschnitt 4/5 nur fuer
 * die Rolle `Admin` erreichbar (Navigationspunkt in `app.html` ist nur fuer Admin sichtbar,
 * Backend erzwingt die Rolle serverseitig).
 */

function unique(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 10_000)}`;
}

test.beforeEach(async ({ page }) => {
  await loginViaUi(page, urls.crmWeb, seedUsers.admin.email, seedUsers.admin.password);
  await page.goto(`${urls.crmWeb}/catalog`);
  await settleAfterNavigation(page);
  await expect(page).toHaveURL(new RegExp(`${urls.crmWeb}/catalog$`));
});

test('Leistung mit gueltigem Namen anlegen erscheint in der Leistungsliste', async ({ page }) => {
  const name = unique('Beratung');

  await page.getByRole('link', { name: /neue leistung|new service/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#name').fill(name);
  await page.locator('button[type=submit]').click();

  await expect(page).toHaveURL(new RegExp(`${urls.crmWeb}/catalog$`));
  await page.locator('#service-search').fill(name);
  await expect(page.getByText(name, { exact: true }).first()).toBeVisible();
});

test('Leistung ohne Namen anlegen zeigt Validierungsfehler und persistiert nichts', async ({ page }) => {
  await page.getByRole('link', { name: /neue leistung|new service/i }).click();
  await settleAfterNavigation(page);
  await page.locator('button[type=submit]').click();

  // Verbleibt auf dem Formular (kein Redirect zur Liste) - Validierungsfehler sichtbar.
  await expect(page).toHaveURL(new RegExp(`${urls.crmWeb}/catalog/new`));
  await expect(page.getByRole('alert').first()).toBeVisible();
});

test('Suche nach Namen filtert die Leistungsliste', async ({ page }) => {
  const name = unique('Haarschnitt');

  await page.getByRole('link', { name: /neue leistung|new service/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#name').fill(name);
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.crmWeb}/catalog$`));

  await page.locator('#service-search').fill(unique('nicht-vorhanden-xyz'));
  await expect(page.getByText(name, { exact: true }).first()).not.toBeVisible();

  await page.locator('#service-search').fill(name);
  await expect(page.getByText(name, { exact: true }).first()).toBeVisible();
});

test('Leistung bearbeiten persistiert die Aenderungen', async ({ page }) => {
  const name = unique('Massage');
  const updatedDescription = 'Entspannende Ganzkoerpermassage, 60 Minuten.';

  await page.getByRole('link', { name: /neue leistung|new service/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#name').fill(name);
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.crmWeb}/catalog$`));

  await page.locator('#service-search').fill(name);
  const row = page.locator('tr', { hasText: name });
  await expect(row).toHaveCount(1);
  await row.getByRole('link', { name: /bearbeiten|edit/i }).click();
  await settleAfterNavigation(page);

  await page.locator('#description').fill(updatedDescription);
  await page.locator('button[type=submit]').click();

  await expect(page).toHaveURL(new RegExp(`${urls.crmWeb}/catalog$`));
  await page.locator('#service-search').fill(name);
  await expect(page.getByText(updatedDescription)).toBeVisible();
});

test('Leistung deaktivieren markiert sie als inaktiv, bleibt aber ueber den Inaktiv-Filter sichtbar', async ({
  page,
}) => {
  const name = unique('Maniküre');

  await page.getByRole('link', { name: /neue leistung|new service/i }).click();
  await settleAfterNavigation(page);
  await page.locator('#name').fill(name);
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(new RegExp(`${urls.crmWeb}/catalog$`));

  await page.locator('#service-search').fill(name);
  const row = page.locator('tr', { hasText: name });
  await expect(row).toHaveCount(1);
  await row.getByRole('button', { name: /deaktivieren|deactivate/i }).click();

  await page
    .getByRole('dialog')
    .getByRole('button', { name: /deaktivieren|deactivate/i })
    .click();
  await expect(page.getByRole('dialog')).not.toBeVisible();

  // Default-Status-Filter ist "Aktiv" (siehe `ServiceCatalogStateService`) - die deaktivierte
  // Leistung darf dort nicht mehr erscheinen.
  await expect(row).toHaveCount(0);

  await page.locator('#service-status-filter').selectOption('inactive');
  await page.locator('#service-search').fill(name);
  const inactiveRow = page.locator('tr', { hasText: name });
  await expect(inactiveRow).toHaveCount(1);
  // Scoped auf die Zeile (nicht `page.getByText(...)`), da der Status-Filter selbst ein
  // `<select>` mit einer "Inaktiv"-`<option>` enthaelt, die sonst ebenfalls (als verstecktes
  // DOM-Element) matchen wuerde - Testcode-Praezisierung, kein Produktbug.
  await expect(inactiveRow.getByText(/inaktiv|inactive/i)).toBeVisible();
});
