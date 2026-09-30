import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright-Konfiguration fuer die CRM-E2E-Tests.
 *
 * Diese Suite startet KEINE Server selbst (kein `webServer`-Block): sie erwartet, dass
 * - die vier Backend-Services (identity-service, customer-service, employee-service,
 *   appointment-service) und
 * - die drei Angular-Dev-Server (customer-web, employee-web, appointment-web)
 * bereits laufen (siehe README.md fuer die genauen Start-Kommandos, Aspire AppHost vs.
 * manueller Einzelstart). Alle Basis-URLs sind ueber Umgebungsvariablen konfigurierbar,
 * mit Defaults, die zu den fest hinterlegten Ports aus den Angular-`environment.ts`-Dateien
 * passen (Clients/*-web/src/environments/environment.ts).
 */
export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
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
      name: 'appointment-web',
      testMatch: /(appointments|rbac)\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'auth',
      testMatch: /auth\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
