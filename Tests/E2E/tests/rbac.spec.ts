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
 * Deckt das Requirement "Sichtbarkeit von Terminen nach Rolle (RBAC)" aus
 * `UserStories/terminverwaltung.openspec.md`, Abschnitt 3, ab - der komplexeste
 * Cross-Service-RBAC-Flow im System (Gateway/Identity-Claims -> appointment-service
 * Ownership-Filter -> UI-Fehlerbehandlung).
 *
 * HISTORISCHER HINWEIS ZU BUG 2 (jetzt behoben, siehe Tests/E2E/README.md):
 * Frueher hatte der geseedete `identity-service`-Testnutzer "employee@crm.local" eine
 * deterministische User-Id, die NIE mit einer echten `Employee.Id` aus employee-service
 * uebereinstimmte, wodurch das positive Ownership-Scenario nicht ueber einen echten Login-Flow
 * erzeugbar war. Seit dem Bug-2-Fix (backend-agent, 2026-09-30) existiert in employee-service
 * ein dev-/test-only Seeder (`DevEmployeeSeedHostedService`), der genau einen Employee-Datensatz
 * mit exakt der `identity-service`-User-Id von "employee@crm.local" anlegt
 * (`seedUsers.employee.id` in `fixtures/env.ts`). Der positive "sieht eigene Termine"-Fall ist
 * daher jetzt als echter Test aktiviert (siehe unten).
 */

// serviceId muss eine echte Guid sein (EmployeeServiceAssignmentDto.ServiceId: Guid) -
// ein sprechender String wie zuvor "e2e-rbac" wird von System.Text.Json mit 400
// "The JSON value could not be converted" abgelehnt (Testcode-Fix, kein Produktbug).
const SERVICE = { serviceId: '33333333-3333-3333-3333-333333333333', serviceName: 'E2E RBAC Leistung' };

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
  await settleAfterNavigation(page);
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
  // Seit der Frontend-Konsolidierung landet der Login IMMER auf `/customers` (App-Default) -
  // explizit zum Termine-Bereich navigieren (Testcode-Korrektur, kein Produktbug).
  await page.goto(`${urls.appointmentWeb}/appointments`);
  await settleAfterNavigation(page);
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

test('Employee sieht eigene Termine in der Kalenderansicht (Ownership-Positivfall, Bug 2 behoben)', async ({ page }) => {
  const admin = await loginAsAdmin();

  // Der dev-geseedete Employee-Datensatz mit exakt der identity-service-User-Id von
  // "employee@crm.local" existiert bereits beim Service-Start (DevEmployeeSeedHostedService) -
  // hier nur Leistung + Arbeitszeit fuer diesen bestehenden Datensatz ergaenzen (kein erneutes
  // Anlegen, sonst 409/Email-Konflikt).
  const ownEmployee = {
    id: seedUsers.employee.id,
    firstName: 'CRM',
    lastName: 'Employee',
    email: seedUsers.employee.email,
    role: 'Employee' as const,
  };
  await assignService(admin.accessToken, ownEmployee, SERVICE);
  await setWorkingHours(admin.accessToken, ownEmployee.id, [
    { weekday: 'Monday', startTime: '09:00:00', endTime: '17:00:00' },
  ]);
  const customer = await createCustomer(admin.accessToken, { firstName: 'RBAC', lastName: unique('EigenerKunde') });
  const monday = nextWeekday(1);
  const dateIso = toDateInputValue(monday);
  const appointment = await createAppointment(admin.accessToken, {
    customerId: customer.id,
    employeeId: ownEmployee.id,
    serviceId: SERVICE.serviceId,
    start: `${dateIso}T13:00:00`,
    end: `${dateIso}T14:00:00`,
  });
  expect(appointment, 'Vorbedingung: eigener Termin muss anlegbar sein').not.toBeNull();

  await loginViaUi(page, urls.appointmentWeb, seedUsers.employee.email, seedUsers.employee.password);
  // Seit der Frontend-Konsolidierung landet der Login IMMER auf `/customers` (App-Default) -
  // explizit zum Termine-Bereich navigieren (Testcode-Korrektur, kein Produktbug).
  await page.goto(`${urls.appointmentWeb}/appointments`);
  await settleAfterNavigation(page);
  await expect(page.locator('#employee-filter')).not.toBeVisible();
  await page.getByRole('tab', { name: /woche|week/i }).click();
  await page.getByRole('button', { name: /n[äa]chste|next/i }).click();
  await expect(page.getByText('13:00')).toBeVisible();
});

test('Employee-Versuch, Termin eines anderen Mitarbeiters zu verschieben, wird mit Fehlermeldung im UI abgelehnt (403)', async ({
  page,
}) => {
  const admin = await loginAsAdmin();
  const { appointment, dateIso } = await setupEmployeeWithAppointment(admin.accessToken, 'FremdVerschieben', '14:00', '15:00');

  await loginViaUi(page, urls.appointmentWeb, seedUsers.employee.email, seedUsers.employee.password);
  await page.goto(`${urls.appointmentWeb}/appointments/${appointment.id}`);
  await settleAfterNavigation(page);

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
  await settleAfterNavigation(page);

  await page.getByRole('button', { name: /stornieren|cancel/i }).first().click();
  await page.locator('#cancel-reason').fill('Fremder Storno-Versuch');
  await page.getByRole('dialog').getByRole('button', { name: /stornieren|cancel/i }).click();

  await expect(page.getByRole('alert').filter({ hasText: /berechtigung|allowed|forbidden/i }).first()).toBeVisible();

  const check = await fetch(`${urls.appointmentApi}/appointments/${appointment.id}`, {
    headers: { Authorization: `Bearer ${admin.accessToken}` },
  });
  const current = await check.json();
  expect(current.status).toBe('Confirmed');
});
