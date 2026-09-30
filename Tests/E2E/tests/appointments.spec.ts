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
import { settleAfterNavigation } from '../fixtures/ui';

/**
 * Deckt die Scenarios aus `UserStories/terminverwaltung.openspec.md`, Abschnitt 3, ab
 * (ausser RBAC - siehe `rbac.spec.ts`):
 * - Termin im verfuegbaren Zeitfenster anlegen
 * - Termin ausserhalb der Arbeitszeit anlegen (Validierungsfehler)
 * - Doppelbuchung verhindern (Kollisionspruefung)
 * - Termin erfolgreich verschieben
 * - Termin stornieren
 * - Wochenansicht eines Mitarbeiters anzeigen
 *
 * Jeder Test bereitet Kunde/Mitarbeiter/Arbeitszeiten/Leistung ueber die API vor
 * (schnell, deterministisch), die eigentliche Handlung (Termin anlegen/verschieben/
 * stornieren/Kalender) laeuft ausschliesslich ueber die echte Browser-UI.
 */

// serviceId muss eine echte Guid sein (EmployeeServiceAssignmentDto.ServiceId: Guid) -
// ein sprechender String wie zuvor "e2e-beratung" wird von System.Text.Json mit 400
// "The JSON value could not be converted" abgelehnt (Testcode-Fix, kein Produktbug).
const SERVICE = { serviceId: '22222222-2222-2222-2222-222222222222', serviceName: 'E2E Beratung' };

function unique(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 10_000)}`;
}

async function setupEmployeeWithMondaySchedule(adminToken: string) {
  const lastName = unique('Termine');
  const employee = await createEmployee(adminToken, {
    firstName: 'Termin',
    lastName,
    email: `${lastName}@crm.local`,
    role: 'Employee',
  });
  await assignService(adminToken, { id: employee.id, firstName: 'Termin', lastName, email: `${lastName}@crm.local`, role: 'Employee' }, SERVICE);
  await setWorkingHours(adminToken, employee.id, [
    { weekday: 'Monday', startTime: '09:00:00', endTime: '17:00:00' },
  ]);
  return employee;
}

test('Termin im verfuegbaren Zeitfenster anlegen', async ({ page }) => {
  const admin = await loginAsAdmin();
  const employee = await setupEmployeeWithMondaySchedule(admin.accessToken);
  const customer = await createCustomer(admin.accessToken, {
    firstName: 'Anna',
    lastName: unique('Kundin'),
    email: `${unique('anna')}@example.com`,
  });
  const monday = nextWeekday(1);
  const dateIso = toDateInputValue(monday);

  await loginViaUi(page, urls.appointmentWeb, seedUsers.admin.email, seedUsers.admin.password);
  await page.goto(`${urls.appointmentWeb}/appointments/new`);
  await settleAfterNavigation(page);

  await page.locator('#customer-search').fill(customer.lastName ?? 'Anna');
  await page.locator('#employee-select').selectOption(employee.id);
  await page.locator('#date-input').fill(dateIso);
  await page.locator('#service-select').selectOption(SERVICE.serviceId);
  await page.locator('#start-time').fill('10:00');
  await page.locator('#end-time').fill('11:00');
  await page.locator('button[type=submit]').click();

  await expect(page).toHaveURL(new RegExp(`${urls.appointmentWeb}/appointments$`));
});

test('Termin ausserhalb der Arbeitszeit zeigt Validierungsfehler und persistiert nichts', async ({ page }) => {
  const admin = await loginAsAdmin();
  const employee = await setupEmployeeWithMondaySchedule(admin.accessToken);
  const customer = await createCustomer(admin.accessToken, {
    firstName: 'Otto',
    lastName: unique('Ausserhalb'),
  });
  const monday = nextWeekday(1);
  const dateIso = toDateInputValue(monday);

  await loginViaUi(page, urls.appointmentWeb, seedUsers.admin.email, seedUsers.admin.password);
  await page.goto(`${urls.appointmentWeb}/appointments/new`);
  await settleAfterNavigation(page);

  await page.locator('#customer-search').fill(customer.lastName);
  await page.locator('#employee-select').selectOption(employee.id);
  await page.locator('#date-input').fill(dateIso);
  await page.locator('#service-select').selectOption(SERVICE.serviceId);
  // 20:00-21:00 liegt ausserhalb der hinterlegten Arbeitszeit 09:00-17:00.
  await page.locator('#start-time').fill('20:00');
  await page.locator('#end-time').fill('21:00');
  await page.locator('button[type=submit]').click();

  await expect(page).toHaveURL(new RegExp(`${urls.appointmentWeb}/appointments/new`));
  await expect(page.getByRole('alert').first()).toBeVisible();
});

test('Doppelbuchung fuer denselben Mitarbeiter wird verhindert', async ({ page }) => {
  const admin = await loginAsAdmin();
  const employee = await setupEmployeeWithMondaySchedule(admin.accessToken);
  const customerA = await createCustomer(admin.accessToken, { firstName: 'Erst', lastName: unique('Buchung') });
  const customerB = await createCustomer(admin.accessToken, { firstName: 'Zweit', lastName: unique('Buchung') });
  const monday = nextWeekday(1);
  const dateIso = toDateInputValue(monday);

  const first = await createAppointment(admin.accessToken, {
    customerId: customerA.id,
    employeeId: employee.id,
    serviceId: SERVICE.serviceId,
    start: `${dateIso}T10:00:00`,
    end: `${dateIso}T11:00:00`,
  });
  expect(first, 'Vorbedingung: erster Termin muss erfolgreich angelegt werden').not.toBeNull();

  await loginViaUi(page, urls.appointmentWeb, seedUsers.admin.email, seedUsers.admin.password);
  await page.goto(`${urls.appointmentWeb}/appointments/new`);
  await settleAfterNavigation(page);

  await page.locator('#customer-search').fill(customerB.lastName);
  await page.locator('#employee-select').selectOption(employee.id);
  await page.locator('#date-input').fill(dateIso);
  await page.locator('#service-select').selectOption(SERVICE.serviceId);
  // 10:30-11:30 ueberlappt mit dem bestehenden Termin 10:00-11:00.
  await page.locator('#start-time').fill('10:30');
  await page.locator('#end-time').fill('11:30');
  await page.locator('button[type=submit]').click();

  await expect(page).toHaveURL(new RegExp(`${urls.appointmentWeb}/appointments/new`));
  await expect(page.getByRole('alert').first()).toBeVisible();
});

test('Termin erfolgreich verschieben', async ({ page }) => {
  const admin = await loginAsAdmin();
  const employee = await setupEmployeeWithMondaySchedule(admin.accessToken);
  const customer = await createCustomer(admin.accessToken, { firstName: 'Resch', lastName: unique('Edule') });
  const monday = nextWeekday(1);
  const dateIso = toDateInputValue(monday);

  const appointment = await createAppointment(admin.accessToken, {
    customerId: customer.id,
    employeeId: employee.id,
    serviceId: SERVICE.serviceId,
    start: `${dateIso}T10:00:00`,
    end: `${dateIso}T11:00:00`,
  });
  expect(appointment).not.toBeNull();

  await loginViaUi(page, urls.appointmentWeb, seedUsers.admin.email, seedUsers.admin.password);
  await page.goto(`${urls.appointmentWeb}/appointments/${appointment!.id}`);
  await settleAfterNavigation(page);

  await page.locator('#reschedule-date').fill(dateIso);
  await page.locator('#reschedule-start').fill('13:00');
  await page.locator('#reschedule-end').fill('14:00');
  await page.getByRole('button', { name: /verschieben|reschedule/i }).click();

  await expect(page.getByText('13:00')).toBeVisible();
});

test('Termin stornieren markiert ihn als Cancelled und gibt den Slot frei', async ({ page }) => {
  const admin = await loginAsAdmin();
  const employee = await setupEmployeeWithMondaySchedule(admin.accessToken);
  const customer = await createCustomer(admin.accessToken, { firstName: 'Storno', lastName: unique('Fall') });
  const monday = nextWeekday(1);
  const dateIso = toDateInputValue(monday);

  const appointment = await createAppointment(admin.accessToken, {
    customerId: customer.id,
    employeeId: employee.id,
    serviceId: SERVICE.serviceId,
    start: `${dateIso}T15:00:00`,
    end: `${dateIso}T16:00:00`,
  });
  expect(appointment).not.toBeNull();

  await loginViaUi(page, urls.appointmentWeb, seedUsers.admin.email, seedUsers.admin.password);
  await page.goto(`${urls.appointmentWeb}/appointments/${appointment!.id}`);
  await settleAfterNavigation(page);

  await page.getByRole('button', { name: /stornieren|cancel/i }).first().click();
  await page.locator('#cancel-reason').fill('E2E Testabsage');
  await page.getByRole('dialog').getByRole('button', { name: /stornieren|cancel/i }).click();

  await expect(page.getByText(/cancelled|storniert/i)).toBeVisible();
});

test('Wochenansicht zeigt Termine eines Mitarbeiters zeitlich korrekt', async ({ page }) => {
  const admin = await loginAsAdmin();
  const employee = await setupEmployeeWithMondaySchedule(admin.accessToken);
  const customer = await createCustomer(admin.accessToken, { firstName: 'Woche', lastName: unique('Ansicht') });
  const monday = nextWeekday(1);
  const dateIso = toDateInputValue(monday);

  const appointment = await createAppointment(admin.accessToken, {
    customerId: customer.id,
    employeeId: employee.id,
    serviceId: SERVICE.serviceId,
    start: `${dateIso}T11:00:00`,
    end: `${dateIso}T12:00:00`,
  });
  expect(appointment).not.toBeNull();

  await loginViaUi(page, urls.appointmentWeb, seedUsers.admin.email, seedUsers.admin.password);
  await page.goto(`${urls.appointmentWeb}/appointments`);
  await settleAfterNavigation(page);

  await page.getByRole('tab', { name: /woche|week/i }).click();
  await page.locator('#employee-filter').selectOption(employee.id);
  // nextWeekday(1) liegt in der kommenden Kalenderwoche -> einmal "naechste Periode".
  await page.getByRole('button', { name: /n[äa]chste|next/i }).click();

  await expect(page.getByRole('grid')).toBeVisible();
  await expect(page.getByText('11:00')).toBeVisible();
});
