/**
 * Schlankes Domain-Modell fuer die Terminhistorie in der Kundendetailsicht. Bewusst kein
 * Cross-Import aus dem `appointments`-Feature-Bereich (analog `appointments/domain/customer.model.ts`,
 * ADR 0001) - nur die fuer die Anzeige benoetigten Felder aus `AppointmentListItemDto`
 * (`appointment-service`).
 */
export type AppointmentHistoryStatus = 'Confirmed' | 'Cancelled' | 'Completed' | 'RequiresReview';

export interface AppointmentHistoryEntry {
  id: string;
  date: string;
  startTime: string;
  endTime: string;
  status: AppointmentHistoryStatus;
}
