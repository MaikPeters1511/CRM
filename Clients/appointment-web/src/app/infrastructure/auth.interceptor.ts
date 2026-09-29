import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthTokenStorage } from './auth-token.storage';

/**
 * Functional HTTP-Interceptor:
 * - haengt das JWT als Bearer-Token an jeden Request an, der an die eigene API-Basis-URL
 *   (appointment-service/customer-service/employee-service/identity-service via Gateway) geht.
 *   Requests an fremde Origins (z.B. i18n-JSON-Dateien) bleiben unangetastet.
 * - leitet bei 401 (abgelaufene/ungueltige Session) automatisch zur Login-Seite um.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenStorage = inject(AuthTokenStorage);
  const router = inject(Router);
  const token = tokenStorage.getToken();

  const request =
    token && !req.url.startsWith('/i18n/')
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        tokenStorage.clear();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
  );
};
