import { AppointmentStatus } from '../../appointments/domain/appointment.model';

/**
 * Zustandslose Kennzahlen fuer die Dashboard-Kacheln. Werden clientseitig aus bereits
 * vorhandenen Backend-Endpunkten aggregiert (siehe
 * UserStories/mp-crm-mockup-redesign.openspec.md, Abschnitt "Design"); keine neuen
 * Backend-Endpunkte. Kennzahlen, die ohne neuen Endpunkt nicht sauber ableitbar sind
 * (z. B. "neue Kunden diesen Monat", "Team-Auslastung"), werden bewusst weggelassen.
 */
export interface DashboardMetrics {
  appointmentsToday: number;
  openRequests: number;
  activeCustomers: number;
  totalEmployees: number;
}

/** Eintrag der Liste "Anstehende Termine", reduziert auf die fuer die Vorschau relevanten Felder. */
export interface UpcomingAppointmentItem {
  id: string;
  start: string;
  end: string;
  status: AppointmentStatus;
}
