import { HttpErrorResponse } from '@angular/common/http';

/** Siehe `settings/infrastructure/api-error.util.ts` - identisches Muster. */
export function isBackendUnavailable(error: unknown): boolean {
  return (
    error instanceof HttpErrorResponse &&
    (error.status === 0 || error.status === 404 || error.status === 503)
  );
}
