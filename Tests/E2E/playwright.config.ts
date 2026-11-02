import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright-Konfiguration fuer die CRM-E2E-Tests.
 *
 * Diese Suite startet KEINE Server selbst (kein `webServer`-Block): sie erwartet, dass
 * - die vier Backend-Services (identity-service, customer-service, employee-service,
 *   appointment-service) und
 * - das eine konsolidierte Angular-Frontend (`Clients/crm-web`, siehe
 *   `docs/adr/0002-frontend-konsolidierung.md`)
 * bereits laufen (siehe README.md fuer die genauen Start-Kommandos, Aspire AppHost vs.
 * manueller Einzelstart). Alle Basis-URLs sind ueber Umgebungsvariablen konfigurierbar,
 * mit Defaults, die zum fest hinterlegten Port aus `Clients/crm-web/src/environments/environment.ts`
 * passen. Die Projektnamen unten (`customer-web`/`employee-web`/`appointment-web`) sind
 * historisch gewachsene Labels zur Testgruppierung nach Feature-Bereich, nicht mehr eigene Apps -
 * `urls.customerWeb`/`urls.employeeWeb`/`urls.appointmentWeb` zeigen alle auf dieselbe
 * `crm-web`-Basis-URL (siehe `fixtures/env.ts`).
 */
export default defineConfig({
  testDir: './tests',
  // 90s statt 30s: der korrekt funktionierende Brute-Force-Schutz von identity-service
  // (5 Logins/Minute/IP, siehe README.md "Rate-Limiting-Hinweis") kann bei vielen
  // aufeinanderfolgenden Tests einen Retry-Wait von bis zu 61s in `loginViaUi`/`loginViaApi`
  // ausloesen (siehe fixtures/auth.ts, fixtures/api.ts) - kein Produktivcode-Fix, reine
  // Test-Timeout-Anpassung an ein bewusstes Sicherheitsfeature.
  timeout: 200_000,
  expect: { timeout: 5_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ignoreHTTPSErrors: true,
  },
  projects: [
    {
      name: 'customer-web',
      testMatch: /customers\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'employee-web',
      testMatch: /employees\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'catalog',
      testMatch: /catalog\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'appointment-web',
      testMatch: /(appointments|rbac)\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'auth',
      testMatch: /auth\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'assistant',
      testMatch: /assistant\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
