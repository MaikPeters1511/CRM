/**
 * Zentrale Konfiguration aller Basis-URLs fuer die E2E-Suite.
 *
 * Seit der Frontend-Konsolidierung (siehe `docs/adr/0002-frontend-konsolidierung.md` und
 * `UserStories/frontend-konsolidierung.openspec.md`) existiert nur noch ein einziges
 * Angular-Frontend `Clients/crm-web` (Dev-Port 4200) statt vormals drei getrennter Apps
 * (`customer-web` 4200, `employee-web` 4201, `appointment-web` 4202). `customerWeb`,
 * `employeeWeb` und `appointmentWeb` zeigen daher bewusst auf dieselbe Basis-URL (`crmWeb`) –
 * die Aliase bleiben erhalten, damit die bestehenden Spec-Dateien unveraendert funktionieren
 * (`urls.customerWeb`, `urls.employeeWeb`, `urls.appointmentWeb` referenzieren weiterhin die
 * jeweiligen Feature-Bereiche `/customers`, `/employees`, `/appointments` derselben App).
 *
 * Backend-API-Basis-URLs entsprechen weiterhin den `launchSettings.json`-HTTP-Profilen der
 * Backend-Services (siehe README.md). Alles ist per Umgebungsvariable ueberschreibbar, z.B.
 * wenn die Services ueber das Aspire AppHost mit dynamisch vergebenen Ports laufen.
 */
function env(name: string, fallback: string): string {
  return process.env[name]?.trim() || fallback;
}

const crmWeb = env('E2E_CRM_WEB_URL', 'http://localhost:4200');

export const urls = {
  crmWeb,
  customerWeb: crmWeb,
  employeeWeb: crmWeb,
  appointmentWeb: crmWeb,

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
