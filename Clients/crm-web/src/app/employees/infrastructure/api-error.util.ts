import { mapBackendErrors } from '../../shared/errors/backend-error-messages';
import { HttpErrorResponse } from '@angular/common/http';
import { ExistingAppointmentSlot } from '../domain/employee.model';

/**
 * Extrahiert eine saubere, fuer Endnutzer geeignete Fehlermeldung aus einer
 * HttpErrorResponse. Liefert NIEMALS Stacktraces oder interne Server-Details
 * an die UI zurueck (siehe CLAUDE.md, Error Handling & Logging).
 */
export function extractValidationErrors(error: unknown): string[] {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { errors?: Record<string, string[]> | string[] } | null;
    if (Array.isArray(body?.errors)) {
      return mapBackendErrors(body.errors);
    }
    if (body?.errors) {
      return mapBackendErrors(Object.values(body.errors).flat());
    }
  }
  return [];
}

/**
 * Extrahiert die kollidierenden Termine aus einer 409-Conflict-Antwort beim
 * Anlegen einer Abwesenheit (siehe `AddAbsenceResult.ConflictingAppointments`).
 */
export function extractConflictingAppointments(error: unknown): ExistingAppointmentSlot[] {
  if (error instanceof HttpErrorResponse && error.status === 409) {
    const body = error.error as { conflictingAppointments?: ExistingAppointmentSlot[] } | null;
    return body?.conflictingAppointments ?? [];
  }
  return [];
}

export function isConflict(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 409;
}

/** Fallback-Key fuer i18n, wenn keine spezifische Fehlermeldung vorliegt. */
export const GENERIC_ERROR_KEY = 'employee.errors.generic';
