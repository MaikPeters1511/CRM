import { urls, seedUsers } from './env';

/**
 * Duenne API-Test-Helfer (Node-`fetch`, kein Playwright-`request`-Fixture noetig), um
 * Testdaten schnell und deterministisch ausserhalb der UI vorzubereiten (z.B. einen zweiten
 * Termin fuer den RBAC-Test anzulegen), analog zum "API-first, UI-second"-Muster fuer
 * robuste, schnelle E2E-Tests. Die UI-Scenarios selbst (Login, Formulare, Fehlermeldungen)
 * werden weiterhin ausschliesslich ueber echte Browser-Interaktionen in den *.spec.ts-Dateien
 * abgedeckt.
 */

export interface LoginResult {
  accessToken: string;
  role: 'Admin' | 'Employee';
  userId: string;
  displayName: string;
}

/**
 * Retry bei 429: siehe ausfuehrlichen Kommentar in `fixtures/auth.ts` (`loginViaUi`) - derselbe
 * korrekt funktionierende Brute-Force-Schutz (5 Versuche/Minute/IP) greift hier ebenfalls, da
 * viele Tests zusaetzlich per API einloggen (Testdaten-Vorbereitung "API-first").
 */
export async function loginViaApi(email: string, password: string): Promise<LoginResult> {
  for (let attempt = 1; attempt <= 3; attempt++) {
    const response = await fetch(`${urls.identityApi}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });
    if (response.status === 429 && attempt < 3) {
      await new Promise((resolve) => setTimeout(resolve, 61_000));
      continue;
    }
    if (!response.ok) {
      throw new Error(`Login fehlgeschlagen fuer ${email}: HTTP ${response.status}`);
    }
    return response.json();
  }
  throw new Error(`Login fehlgeschlagen fuer ${email}: wiederholt HTTP 429`);
}

export async function loginAsAdmin(): Promise<LoginResult> {
  return loginViaApi(seedUsers.admin.email, seedUsers.admin.password);
}

function authHeaders(token: string): Record<string, string> {
  return { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` };
}

export async function createEmployee(
  token: string,
  body: { firstName: string; lastName: string; email: string; role: 'Admin' | 'Employee' },
): Promise<{ id: string }> {
  const response = await fetch(`${urls.employeeApi}/employees`, {
    method: 'POST',
    headers: authHeaders(token),
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(`createEmployee fehlgeschlagen: HTTP ${response.status} ${await response.text()}`);
  }
  return response.json();
}

export async function assignService(
  token: string,
  employee: { id: string; firstName: string; lastName: string; email: string; role: 'Admin' | 'Employee' },
  service: { serviceId: string; serviceName: string },
): Promise<void> {
  const response = await fetch(`${urls.employeeApi}/employees/${employee.id}`, {
    method: 'PUT',
    headers: authHeaders(token),
    body: JSON.stringify({
      firstName: employee.firstName,
      lastName: employee.lastName,
      email: employee.email,
      role: employee.role,
      services: [service],
    }),
  });
  if (!response.ok) {
    throw new Error(`assignService fehlgeschlagen: HTTP ${response.status} ${await response.text()}`);
  }
}

export async function setWorkingHours(
  token: string,
  employeeId: string,
  slots: Array<{ weekday: string; startTime: string; endTime: string }>,
): Promise<void> {
  const response = await fetch(`${urls.employeeApi}/employees/${employeeId}/schedule`, {
    method: 'PUT',
    headers: authHeaders(token),
    body: JSON.stringify({ slots }),
  });
  if (!response.ok) {
    throw new Error(`setWorkingHours fehlgeschlagen: HTTP ${response.status} ${await response.text()}`);
  }
}

export async function createCustomer(
  token: string,
  body: { firstName: string; lastName: string; email?: string; phone?: string },
): Promise<{ id: string }> {
  const response = await fetch(`${urls.customerApi}/customers`, {
    method: 'POST',
    headers: authHeaders(token),
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(`createCustomer fehlgeschlagen: HTTP ${response.status} ${await response.text()}`);
  }
  return response.json();
}

export async function createAppointment(
  token: string,
  body: {
    customerId: string;
    employeeId: string;
    serviceId: string;
    start: string;
    end: string;
  },
): Promise<{ id: string } | null> {
  const response = await fetch(`${urls.appointmentApi}/appointments`, {
    method: 'POST',
    headers: authHeaders(token),
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    return null;
  }
  return response.json();
}

/** Naechster Montag (ISO-Datum, yyyy-MM-dd) ab heute, damit Arbeitszeiten/Termine stets in der Zukunft liegen. */
export function nextWeekday(targetIsoWeekday: number): Date {
  const now = new Date();
  const date = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 7);
  const currentIsoWeekday = date.getDay() === 0 ? 7 : date.getDay();
  const diff = (targetIsoWeekday - currentIsoWeekday + 7) % 7;
  date.setDate(date.getDate() + diff);
  return date;
}

/**
 * Formatiert ein lokales Datum (z.B. aus `nextWeekday()`, lokale Mitternacht) als `yyyy-MM-dd`.
 * WICHTIG: bewusst NICHT `date.toISOString().slice(0, 10)` (Testcode-Fix, kein Produktbug) -
 * `toISOString()` konvertiert zuerst nach UTC, was in jeder Zeitzone mit positivem UTC-Offset
 * (z.B. Europe/Berlin, UTC+1/+2) den Kalendertag um einen Tag zurueckdreht (lokale Mitternacht
 * Montag wird zu "Sonntag 22:00/23:00 UTC"). Dieser Bug wurde real reproduziert: `nextWeekday(1)`
 * lieferte ein korrektes lokales "Montag 00:00"-Date-Objekt, aber `toISOString().slice(0, 10)`
 * daraus einen Sonntags-Kalendertag - wodurch alle Folgeaufrufe (Arbeitszeiten/Termine) auf
 * einen Tag ohne hinterlegte Arbeitszeit trafen. Fix: lokale Datumskomponenten direkt verwenden.
 */
export function toDateInputValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
