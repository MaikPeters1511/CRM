import { Appointment, AppointmentListItem, CalendarViewMode } from './appointment.model';

/**
 * Reine, zustandslose Hilfsfunktionen zur Berechnung des Kalenderausschnitts
 * (Tag/Woche/Monat) sowie zur Umwandlung der vollen Termindaten (`Appointment`, z.B. nach
 * Anlegen/Verschieben) in den flachen Listeneintrag (`AppointmentListItem`) der Kalenderansicht.
 */
export function getCalendarRange(
  viewMode: CalendarViewMode,
  anchorDate: string,
): { from: string; to: string } {
  const anchor = parseDateOnly(anchorDate);

  switch (viewMode) {
    case 'day':
      return { from: anchorDate, to: anchorDate };
    case 'week': {
      const weekday = anchor.getUTCDay(); // 0 = Sonntag
      const mondayOffset = (weekday + 6) % 7;
      const monday = addDays(anchor, -mondayOffset);
      const sunday = addDays(monday, 6);
      return { from: formatDateOnly(monday), to: formatDateOnly(sunday) };
    }
    case 'month': {
      const firstOfMonth = new Date(Date.UTC(anchor.getUTCFullYear(), anchor.getUTCMonth(), 1));
      const lastOfMonth = new Date(Date.UTC(anchor.getUTCFullYear(), anchor.getUTCMonth() + 1, 0));
      return { from: formatDateOnly(firstOfMonth), to: formatDateOnly(lastOfMonth) };
    }
  }
}

export function shiftAnchorDate(
  viewMode: CalendarViewMode,
  anchorDate: string,
  direction: 1 | -1,
): string {
  const anchor = parseDateOnly(anchorDate);
  switch (viewMode) {
    case 'day':
      return formatDateOnly(addDays(anchor, direction));
    case 'week':
      return formatDateOnly(addDays(anchor, direction * 7));
    case 'month': {
      const shifted = new Date(Date.UTC(anchor.getUTCFullYear(), anchor.getUTCMonth() + direction, 1));
      return formatDateOnly(shifted);
    }
  }
}

export function toListItem(appointment: Appointment): AppointmentListItem {
  return {
    id: appointment.id,
    customerId: appointment.customerId,
    employeeId: appointment.employeeId,
    serviceId: appointment.serviceId,
    date: appointment.start.slice(0, 10),
    startTime: appointment.start.slice(11, 19),
    endTime: appointment.end.slice(11, 19),
    start: appointment.start,
    end: appointment.end,
    status: appointment.status,
    cancellationReason: appointment.cancellationReason,
  };
}

function parseDateOnly(date: string): Date {
  return new Date(`${date}T00:00:00Z`);
}

function formatDateOnly(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function addDays(date: Date, days: number): Date {
  const result = new Date(date.getTime());
  result.setUTCDate(result.getUTCDate() + days);
  return result;
}
