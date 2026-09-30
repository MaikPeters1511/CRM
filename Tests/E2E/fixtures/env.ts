/**
 * Zentrale Konfiguration aller Basis-URLs fuer die E2E-Suite. Defaults entsprechen den
 * fest hinterlegten Ports aus den Angular `environment.ts`-Dateien (Clients/*-web) bzw.
 * den `launchSettings.json`-HTTP-Profilen der Backend-Services (siehe README.md).
 * Alles ist per Umgebungsvariable ueberschreibbar, z.B. wenn die Services ueber das
 * Aspire AppHost mit dynamisch vergebenen Ports laufen.
 */
function env(name: string, fallback: string): string {
  return process.env[name]?.trim() || fallback;
}

export const urls = {
  customerWeb: env('E2E_CUSTOMER_WEB_URL', 'http://localhost:4200'),
  employeeWeb: env('E2E_EMPLOYEE_WEB_URL', 'http://localhost:4201'),
  appointmentWeb: env('E2E_APPOINTMENT_WEB_URL', 'http://localhost:4202'),

  identityApi: env('E2E_IDENTITY_API_URL', 'http://localhost:5179/api'),
  customerApi: env('E2E_CUSTOMER_API_URL', 'http://localhost:5149/api'),
  employeeApi: env('E2E_EMPLOYEE_API_URL', 'http://localhost:5159/api'),
  appointmentApi: env('E2E_APPOINTMENT_API_URL', 'http://localhost:5169/api'),
};

export const seedUsers = {
  admin: {
    email: env('E2E_ADMIN_EMAIL', 'admin@crm.local'),
    password: env('E2E_ADMIN_PASSWORD', 'Admin123!'),
  },
  employee: {
    email: env('E2E_EMPLOYEE_EMAIL', 'employee@crm.local'),
    password: env('E2E_EMPLOYEE_PASSWORD', 'Employee123!'),
  },
};
