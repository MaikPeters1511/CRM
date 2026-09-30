import { test, expect } from '@playwright/test';
import { urls, seedUsers } from '../fixtures/env';
import { loginViaUi } from '../fixtures/auth';

/**
 * Deckt die Scenarios aus `UserStories/authentifizierung.openspec.md`, Abschnitt 3, ab
 * (soweit UI-relevant; reine Backend-Scenarios wie Rate-Limiting/Ownership sind Gegenstand
 * der Backend-Testsuiten, siehe Tests/IdentityService.Tests bzw. Tests/EmployeeService.Tests).
 * Getestet gegen customer-web (repraesentativ fuer den identischen Login-Flow in allen
 * drei Clients).
 */

test.describe('Login gegen identity-service', () => {
  test('Erfolgreicher Login als Admin', async ({ page }) => {
    await loginViaUi(page, urls.customerWeb, seedUsers.admin.email, seedUsers.admin.password);
    await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers$`));
  });

  test('Erfolgreicher Login als Employee', async ({ page }) => {
    await loginViaUi(page, urls.customerWeb, seedUsers.employee.email, seedUsers.employee.password);
    await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers$`));
  });

  test('Fehlgeschlagener Login mit falschem Passwort', async ({ page }) => {
    await loginViaUi(page, urls.customerWeb, seedUsers.admin.email, 'ganz-falsches-passwort');
    await expect(page.getByRole('alert').first()).toBeVisible();
    await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/login$`));
  });

  test('Fehlgeschlagener Login mit unbekannter E-Mail', async ({ page }) => {
    await loginViaUi(page, urls.customerWeb, 'unbekannt@crm.local', 'irrelevant123');
    await expect(page.getByRole('alert').first()).toBeVisible();
    await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/login$`));
  });
});

test.describe('Session-Handling im Frontend', () => {
  test('Navigation ohne gueltige Session leitet zur Login-Seite um', async ({ page }) => {
    await page.goto(`${urls.customerWeb}/customers`);
    await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/login`));
  });

  test('Automatischer Logout bei 401 (abgelaufenes/manipuliertes Token)', async ({ page }) => {
    await loginViaUi(page, urls.customerWeb, seedUsers.admin.email, seedUsers.admin.password);
    await expect(page).toHaveURL(new RegExp(`${urls.customerWeb}/customers$`));

    // Token im sessionStorage manipulieren (bekannter Storage-Key, siehe AuthTokenStorage),
    // sodass der naechste API-Call mit 401 antwortet.
    const tampered = await page.evaluate(() => {
      const raw = sessionStorage.getItem('crm.auth.token');
      if (!raw) return false;
      sessionStorage.setItem('crm.auth.token', `${raw}tampered`);
      return true;
    });
    expect(tampered).toBe(true);

    await page.reload();
    await page.waitForURL(new RegExp(`${urls.customerWeb}/login`), { timeout: 10_000 });
  });
});
