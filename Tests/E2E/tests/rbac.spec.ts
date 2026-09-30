import { test, expect } from '@playwright/test';
import { urls, seedUsers } from '../fixtures/env';
import { loginViaUi } from '../fixtures/auth';
import {
  loginAsAdmin,
  createEmployee,
  assignService,
  setWorkingHours,
  createCustomer,
  createAppointment,
  nextWeekday,
  toDateInputValue,
} from '../fixtures/api';

/**
 * Deckt das Requirement "Sichtbarkeit von Terminen nach Rolle (RBAC)" aus
 * `UserStories/terminverwaltung.openspec.md`, Abschnitt 3, ab - der komplexeste
 * Cross-Service-RBAC-Flow im System (Gateway/Identity-Claims -> appointment-service
 * Ownership-Filter -> UI-Fehlerbehandlung).
 *
 * WICHTIGER HINWEIS ZU EINER ARCHITEKTUR-LUECKE (siehe QA-Abschlussbericht):
 * Der Ownership-Check in appointment-service vergleicht den `NameIdentifier`-Claim des
 * JWT direkt mit `Appointment.EmployeeId`. Der geseedete `identity-service`-Testnutzer
 * "employee@crm.local" hat aber eine deterministische User-Id, die NIE mit einer echten
 * `Employee.Id` aus employee-service uebereinstimmt (employee-service vergibt beim Anlegen
 * server-seitig eine neue `Guid.NewGuid()`, ohne Moeglichkeit, die Id von aussen vorzugeben).
 * Es gibt aktuell keinen Mechanismus, der einen identity-service-User mit einem
 * employee-service-Employee-Datensatz verknuepft.
 * Ergebnis: der reale Login als "employee@crm.local" kann NIEMALS Termine sehen, die einem
 * per Admin angelegten Employee-Datensatz zugeordnet sind - das negative Verhalten
 * ("sieht keine fremden Termine") ist dadurch immer erfuellt, das positive Verhalten
 * ("sieht die eigenen Termine") kann mit den aktuellen Seed-Daten nicht ueber die echte
 * Login-UI erzeugt werden. Die Tests unten pruefen das tatsaechlich beobachtbare,
 * spezifizierte Verhalten (Negativfall + 403 bei Fremdzugriff + Admin sieht alles) und
 * markieren den positiven "sieht eigene Termine"-Fall explizit als `test.fixme`, bis die
 * Luecke architektonisch geschlossen ist (z.B. durch ein Employee-Self-Registration- oder
 * User<->Employee-Link-Konzept).
 */

const SERVICE = { serviceId: 'e2e-rbac', serviceName: 'E2E RBAC Leistung' };

function unique(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 10_000)}`;
}

async function setupEmployeeWithAppointment(adminToken: string, lastNamePrefix: string, startHour: string, endHour: string) {
  const lastName = unique(lastNamePrefix);
  const employee = await createEmployee(adminToken, {
    firstName: 'RBAC',
    lastName,
    email: `${lastName}@crm.local`,
    role: 'Employee',
  });
  await assignService(
    adminToken,
    { id: employee.id, firstName: 'RBAC', lastName, email: `${lastName}@crm.local`, role: 'Employee' },
    SERVICE,
  );
  await setWorkingHours(adminToken, employee.id, [
    { weekday: 'Monday', startTime: '09:00:00', endTime: '17:00:00' },
  ]);
  const customer = await createCustomer(adminToken, { firstName: 'RBAC', lastName: unique('Kunde') });
  const monday = nextWeekday(1);
  const dateIso = toDateInputValue(monday);
  const appointment = await createAppointment(adminToken, {
    customerId: customer.id,
    employeeId: employee.id,
    serviceId: SERVICE.serviceId,
    start: `${dateIso}T${startHour}:00`,
    end: `${dateIso}T${endHour}:00`,
  });
  expect(appointment, 'Vorbedingung: Termin fuer RBAC-Test muss anlegbar sein').not.toBeNull();
  return { employee, customer, appointment: appointment!, dateIso };
}

test('Admin sieht alle Termine (Gesamtuebersicht ueber mehrere Mitarbeiter)', async ({ page }) => {
  const admin = await loginAsAdmin();
  const { employee: employeeA, dateIso } = await setupEmployeeWithAppointment(admin.accessToken, 'AdminSichtA', '09:00', '10:00');
  const { employee: employeeB } = await setupEmployeeWithAppointment(admin.accessToken, 'AdminSichtB', '11:00', '12:00');

  await loginViaUi(page, urls.appointmentWeb, seedUsers.admin.email, seedUsers.admin.password);
  await page.goto(`${urls.appointmentWeb}/appointments`);
  await page.getByRole('tab', { name: /woche|week/i }).click();
  await page.getByRole('button', { name: /n[äa]chste|next/i }).click();

  // Ohne Mitarbeiterfilter ("Alle Mitarbeiter") sieht der Admin Termine beider Mitarbeiter.
  await expect(page.getByText('09:00')).toBeVisible();
  await page.locator('#employee-filter').selectOption(employeeB.id);
  await expect(page.getByText('11:00')).toBeVisible();
  await page.locator('#employee-filter').selectOption(employeeA.id);
  await expect(page.getByText('09:00')).toBeVisible();
});

test('Employee sieht ueber die Kalenderansicht keine fremden Termine (Negativfall)', async ({ page }) => {
  const admin = await loginAsAdmin();
  const { dateIso } = await setupEmployeeWithAppointment(admin.accessToken, 'FremdTermin', '09:00', '10:00');

  await loginViaUi(page, urls.appointmentWeb, seedUsers.employee.email, seedUsers.employee.password);
  await expect(page).toHaveURL(new RegExp(`${urls.appointmentWeb}/appointments$`));

  // Der eingeloggte Employee hat laut identity-service KEINE Ownership an dem soeben von
  // Admin angelegten Termin (siehe Hinweis oben) - er darf daher unter keinen Umstaenden
  // sichtbar sein. Der Mitarbeiterfilter selbst ist fuer die Rolle Employee ausgeblendet
  // (RBAC Frontend-Teil, siehe terminverwaltung.openspec.md Task 11b).
  await expect(page.locator('#employee-filter')).not.toBeVisible();
  await page.getByRole('tab', { name: /woche|week/i }).click();
  await page.getByRole('button', { name: /n[äa]chste|next/i }).click();
  await expect(page.getByText('09:00')).not.toBeVisible();
});

test.fixme(
  'Employee sieht eigene Termine in der Kalenderansicht (blockiert durch fehlende User<->Employee-Verknuepfung, siehe Hinweis oben)',
  async () => {
    // Sobald identity-service-User und employee-service-Employee-Datensaetze verknuepft sind
    // (z.B. gleiche Id oder expliziter Link), kann dieser Test durch echten Login als
    // "employee@crm.local" + einen fuer diesen Employee angelegten Termin ersetzt/aktiviert werden.
  },
);

test('Employee-Versuch, Termin eines anderen Mitarbeiters zu verschieben, wird mit Fehlermeldung im UI abgelehnt (403)', async ({
  page,
}) => {
  const admin = await loginAsAdmin();
  const { appointment, dateIso } = await setupEmployeeWithAppointment(admin.accessToken, 'FremdVerschieben', '14:00', '15:00');

  await loginViaUi(page, urls.appointmentWeb, seedUsers.employee.email, seedUsers.employee.password);
  await page.goto(`${urls.appointmentWeb}/appointments/${appointment.id}`);

  await page.locator('#reschedule-date').fill(dateIso);
  await page.locator('#reschedule-start').fill('16:00');
  await page.locator('#reschedule-end').fill('17:00');
  await page.getByRole('button', { name: /verschieben|reschedule/i }).click();

  await expect(page.getByRole('alert').filter({ hasText: /berechtigung|allowed|forbidden/i }).first()).toBeVisible();

  // Serverseitig verifizieren, dass der Termin trotz UI-Versuch unveraendert blieb (403, kein Update).
  const check = await fetch(`${urls.appointmentApi}/appointments/${appointment.id}`, {
    headers: { Authorization: `Bearer ${admin.accessToken}` },
  });
  const current = await check.json();
  expect(current.start).toContain('14:00');
});

test('Employee-Versuch, Termin eines anderen Mitarbeiters zu stornieren, wird mit Fehlermeldung im UI abgelehnt (403)', async ({
  page,
}) => {
  const admin = await loginAsAdmin();
  const { appointment } = await setupEmployeeWithAppointment(admin.accessToken, 'FremdStornieren', '09:30', '10:30');

  await loginViaUi(page, urls.appointmentWeb, seedUsers.employee.email, seedUsers.employee.password);
  await page.goto(`${urls.appointmentWeb}/appointments/${appointment.id}`);

  await page.getByRole('button', { name: /stornieren|cancel/i }).first().click();
  await page.locator('#cancel-reason').fill('Fremder Storno-Versuch');
  await page.getByRole('button', { name: /best[äa]tig|confirm/i }).click();

  await expect(page.getByRole('alert').filter({ hasText: /berechtigung|allowed|forbidden/i }).first()).toBeVisible();

  const check = await fetch(`${urls.appointmentApi}/appointments/${appointment.id}`, {
    headers: { Authorization: `Bearer ${admin.accessToken}` },
  });
  const current = await check.json();
  expect(current.status).toBe('Confirmed');
});
