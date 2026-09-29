import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extrahiert eine saubere, für Endnutzer geeignete Fehlermeldung aus einer
 * HttpErrorResponse. Liefert NIEMALS Stacktraces oder interne Server-Details
 * an die UI zurueck (siehe CLAUDE.md, Error Handling & Logging).
 */
export function extractValidationErrors(error: unknown): string[] {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { errors?: Record<string, string[]> } | null;
    if (body?.errors) {
      return Object.values(body.errors).flat();
    }
  }
  return [];
}

/** Fallback-Key fuer i18n, wenn keine spezifische Fehlermeldung vorliegt. */
export const GENERIC_ERROR_KEY = 'customer.errors.generic';
