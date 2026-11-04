import { mapBackendErrors } from '../../shared/errors/backend-error-messages';
import { HttpErrorResponse } from '@angular/common/http';

/**
 * Entspricht `customers/infrastructure/api-error.util.ts`: liefert niemals Stacktraces/interne
 * Details an die UI zurueck (siehe CLAUDE.md, Error Handling & Logging). Erkennt zusaetzlich
 * den Fall "Backend-Endpunkt existiert noch nicht" (0/404/503), da die KI- und
 * Security-Settings-APIs waehrend dieser Session parallel in anderen Branches entstehen.
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

/** True, wenn der Endpunkt (noch) nicht erreichbar ist statt eines fachlichen Fehlers. */
export function isBackendUnavailable(error: unknown): boolean {
  return (
    error instanceof HttpErrorResponse &&
    (error.status === 0 || error.status === 404 || error.status === 503)
  );
}
