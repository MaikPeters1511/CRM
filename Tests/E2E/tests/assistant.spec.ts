import { test, expect } from '@playwright/test';
import { urls, seedUsers } from '../fixtures/env';
import { loginViaUi } from '../fixtures/auth';

/**
 * Deckt ausgewaehlte Scenarios aus `UserStories/ki-terminassistent.openspec.md`, Abschnitt 3, ab:
 * - "Employee fragt nach Slot eines anderen Mitarbeiters" (RBAC-Negativtest, echter Backend-Pfad)
 * - "OpenAI-API antwortet mit Rate-Limit" (Resilienz-Szenario)
 *
 * Fuer das Resilienz-Szenario wird der Chat-Endpoint per `page.route` gemockt: ein echter
 * OpenAI-/Qdrant-Ausfall laesst sich aus einer Playwright-Suite heraus nicht zuverlaessig und
 * ohne Seiteneffekte auf andere Tests provozieren. Das fachliche Verhalten von `ai-service`
 * bei Nichtverfuegbarkeit (Polly-Retry, Fehlermeldung statt Absturz) ist bereits durch
 * `Tests/AiService.Tests` (Application-Schicht) abgedeckt; diese E2E-Schicht prueft
 * ausschliesslich, dass das Frontend die vom Backend bei einem Fehler gelieferte Antwort korrekt
 * anzeigt, statt die Anfrage unbeantwortet oder die Anwendung instabil zu lassen. Das RBAC-
 * Scenario oben laeuft dagegen bewusst ungemockt gegen den echten Backend-Pfad (Claim ->
 * Controller -> Wolverine-Handler -> RBAC-Filter).
 */

test.beforeEach(async ({ page }) => {
  await page.goto(urls.crmWeb);
});

test('Employee fragt im Chat nach Slot eines anderen Mitarbeiters - Assistent beschraenkt auf eigenen Kalender', async ({
  page,
}) => {
  await loginViaUi(page, urls.crmWeb, seedUsers.employee.email, seedUsers.employee.password);
  await expect(page).toHaveURL(new RegExp(`${urls.crmWeb}/customers$`));

  await page.getByRole('button', { name: /terminassistent öffnen|open appointment assistant/i }).click();

  const chatInput = page.getByLabel(/nachricht an den terminassistenten|message to the appointment assistant/i);
  await chatInput.fill('Wann hat Mitarbeiter B nächste Woche Zeit für einen Haarschnitt?');
  await chatInput.press('Enter');

  const log = page.getByRole('log');
  await expect(log.getByText(/eigenen kalender|own calendar/i)).toBeVisible({ timeout: 15_000 });
  await expect(log.getByRole('button', { name: /termin anlegen|create appointment/i })).toHaveCount(0);
});

test('Assistent zeigt bei Nichtverfügbarkeit der KI-API eine klare Fehlermeldung statt abzustürzen', async ({
  page,
}) => {
  await page.route('**/api/assistant/chat', async (route) => {
    await route.fulfill({ status: 503, contentType: 'application/json', body: JSON.stringify({}) });
  });

  await loginViaUi(page, urls.crmWeb, seedUsers.admin.email, seedUsers.admin.password);
  await expect(page).toHaveURL(new RegExp(`${urls.crmWeb}/customers$`));

  await page.getByRole('button', { name: /terminassistent öffnen|open appointment assistant/i }).click();

  const chatInput = page.getByLabel(/nachricht an den terminassistenten|message to the appointment assistant/i);
  await chatInput.fill('Freier Termin für Farbe');
  await chatInput.press('Enter');

  await expect(
    page.getByText(/assistent ist aktuell nicht verfügbar|assistant is currently unavailable/i),
  ).toBeVisible();

  // Anwendung bleibt stabil nutzbar - keine abgestuerzte/leere Seite.
  await expect(page.locator('button[aria-expanded="true"]')).toBeVisible();
});
