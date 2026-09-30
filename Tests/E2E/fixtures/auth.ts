import { Page, expect } from '@playwright/test';

/**
 * Fuellt das echte Login-Formular (LoginComponent, identisch in customer-web/employee-web/
 * appointment-web) aus und wartet auf die daraus resultierende Navigation. Bewusst KEIN
 * Bypass ueber sessionStorage/JWT-Injection: der reale Login-Flow gegen identity-service
 * (ueber den jeweiligen Angular-Dev-Server) ist selbst Gegenstand der Scenarios in
 * `authentifizierung.openspec.md`.
 *
 * Retry-Wrapper um den korrekt funktionierenden Brute-Force-Schutz (max. 5 Login-Versuche/
 * Minute/IP, siehe `authentifizierung.openspec.md` und Tests/E2E/README.md,
 * "Rate-Limiting-Hinweis"): eine Test-Suite mit vielen aufeinanderfolgenden Logins (mehrere
 * Spec-Dateien x mehrere Tests) ueberschreitet dieses Limit unvermeidlich, wenn sie ohne
 * Wartezeit gegen den echten Service laeuft. Das Frontend zeigt fuer 429 bewusst dieselbe
 * generische Fehlermeldung wie fuer falsche Zugangsdaten (kein Informationsleck ueber
 * Rate-Limiting) - daher wird hier direkt die Netzwerkantwort auf `POST /auth/login`
 * abgefragt: bei 429 wird bis zum Ablauf des 1-Minuten-Fensters gewartet und der Login-Versuch
 * wiederholt. Kein Aendern von Produktivcode/Rate-Limit-Konfiguration - reine
 * Test-Infrastruktur-Robustheit.
 */
export async function loginViaUi(page: Page, baseUrl: string, email: string, password: string): Promise<void> {
  await page.goto(`${baseUrl}/login`);

  for (let attempt = 1; attempt <= 3; attempt++) {
    await page.locator('#email').fill(email);
    await page.locator('#password').fill(password);

    const responsePromise = page.waitForResponse(
      (response) => response.url().includes('/auth/login') && response.request().method() === 'POST',
    );
    await page.locator('button[type=submit]').click();
    const response = await responsePromise;

    if (response.status() !== 429) {
      return;
    }

    // Rate-Limit-Fenster ist 1 Minute (FixedWindowLimiter) - mit Sicherheitsabstand warten.
    await page.waitForTimeout(61_000);
  }
}

export async function expectLoggedIn(page: Page, baseUrl: string, expectedPathPrefix: string): Promise<void> {
  await expect(page).toHaveURL(new RegExp(`^${baseUrl.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}${expectedPathPrefix}`));
}
