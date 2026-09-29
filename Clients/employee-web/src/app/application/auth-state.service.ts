import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, finalize, of, tap } from 'rxjs';
import { AuthUser, LoginRequest } from '../domain/auth-user.model';
import { AuthApiService } from '../infrastructure/auth-api.service';
import { AuthTokenStorage } from '../infrastructure/auth-token.storage';

/**
 * Application-Layer: zentraler Auth-State (Signals). Haelt den aktuell authentifizierten
 * Nutzer sowie Login-Status/Fehler. Die eigentliche Session (Token) liegt in
 * AuthTokenStorage (sessionStorage); dieser Service ist die In-Memory-Sicht darauf.
 */
@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly api = inject(AuthApiService);
  private readonly tokenStorage = inject(AuthTokenStorage);

  private readonly _currentUser = signal<AuthUser | null>(this.tokenStorage.getUser());
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly currentUser = this._currentUser.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly isAuthenticated = computed(
    () => this._currentUser() !== null && !this.tokenStorage.isExpired(),
  );
  readonly isAdmin = computed(() => this._currentUser()?.role === 'Admin');

  login(request: LoginRequest, onSuccess: () => void): void {
    this._loading.set(true);
    this._error.set(null);

    this.api
      .login(request)
      .pipe(
        tap((response) => {
          const user: AuthUser = {
            userId: response.userId,
            role: response.role,
            displayName: response.displayName,
          };
          this.tokenStorage.save(response.accessToken, user, response.expiresAtUtc);
          this._currentUser.set(user);
          onSuccess();
        }),
        catchError(() => {
          this._error.set('auth.login.errors.invalidCredentials');
          return of(null);
        }),
        finalize(() => this._loading.set(false)),
      )
      .subscribe();
  }

  logout(): void {
    this.tokenStorage.clear();
    this._currentUser.set(null);
  }
}
