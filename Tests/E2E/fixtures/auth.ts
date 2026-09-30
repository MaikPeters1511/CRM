import { Page, expect } from '@playwright/test';

/**
 * Fuellt das echte Login-Formular (LoginComponent, identisch in customer-web/employee-web/
 * appointment-web) aus und wartet auf die daraus resultierende Navigation. Bewusst KEIN
 * Bypass ueber sessionStorage/JWT-Injection: der reale Login-Flow gegen identity-service
 * (ueber den jeweiligen Angular-Dev-Server) ist selbst Gegenstand der Scenarios in
 * `authentifizierung.openspec.md`.
 */
export async function loginViaUi(page: Page, baseUrl: string, email: string, password: string): Promise<void> {
  await page.goto(`${baseUrl}/login`);
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.locator('button[type=submit]').click();
}

export async function expectLoggedIn(page: Page, baseUrl: string, expectedPathPrefix: string): Promise<void> {
  await expect(page).toHaveURL(new RegExp(`^${baseUrl.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}${expectedPathPrefix}`));
}
