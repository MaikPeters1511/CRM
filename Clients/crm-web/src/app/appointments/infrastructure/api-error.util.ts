import { mapBackendErrors } from '../../shared/errors/backend-error-messages';
import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extrahiert eine saubere, fuer Endnutzer geeignete Fehlermeldung aus einer
 * HttpErrorResponse. Liefert NIEMALS Stacktraces oder interne Server-Details
 * an die UI zurueck (siehe CLAUDE.md, Error Handling & Logging).
 *
 * `appointment-service` liefert Fehler in zwei Formen (siehe `AppointmentsController`):
 * - `ValidationProblem` (400): `{ errors: { "": ["..."] } }` (ModelState-Dictionary)
 * - `Conflict`/`NotFound`/`503` (409/404/503): `{ errors: ["..."] }` (flaches Array)
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

export function isConflict(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 409;
}

/**
 * RBAC (terminverwaltung.openspec.md, Abschnitt 3): der Server liefert 403, wenn ein
 * `Employee` versucht, den Termin eines anderen Mitarbeiters zu verschieben/stornieren.
 */
export function isForbidden(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 403;
}

/** Fallback-Key fuer i18n, wenn keine spezifische Fehlermeldung vorliegt. */
export const GENERIC_ERROR_KEY = 'appointment.errors.generic';
