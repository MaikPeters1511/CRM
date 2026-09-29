import { Injectable } from '@angular/core';
import { AuthUser } from '../domain/auth-user.model';

const TOKEN_KEY = 'crm.auth.token';
const USER_KEY = 'crm.auth.user';
const EXPIRES_AT_KEY = 'crm.auth.expiresAtUtc';

/**
 * Kapselt den Zugriff auf sessionStorage fuer das JWT.
 *
 * Bewusst sessionStorage statt localStorage: localStorage ist unbegrenzt lange und
 * ueber alle Tabs/Fenster hinweg persistent zugreifbar, wodurch ein per XSS
 * eingeschleustes Skript ein gestohlenes Token beliebig lange fuer Folgeangriffe
 * nutzen kann. sessionStorage begrenzt das Zeitfenster auf die aktuelle Tab-Session
 * (wird beim Schliessen des Tabs geloescht) und reduziert damit den Schaden bei
 * einem erfolgreichen XSS-Angriff (OWASP A03/A07), ohne das Grundproblem "Token in
 * JS-zugreifbarem Storage" zu loesen (HttpOnly-Cookies waeren staerker, sind aber
 * fuer diesen Entwicklungsstand mit Cross-Origin-Gateway/Services nicht vorgesehen).
 */
@Injectable({ providedIn: 'root' })
export class AuthTokenStorage {
  save(token: string, user: AuthUser, expiresAtUtc: string): void {
    sessionStorage.setItem(TOKEN_KEY, token);
    sessionStorage.setItem(USER_KEY, JSON.stringify(user));
    sessionStorage.setItem(EXPIRES_AT_KEY, expiresAtUtc);
  }

  getToken(): string | null {
    return sessionStorage.getItem(TOKEN_KEY);
  }

  getUser(): AuthUser | null {
    const raw = sessionStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  }

  isExpired(): boolean {
    const expiresAtUtc = sessionStorage.getItem(EXPIRES_AT_KEY);
    if (!expiresAtUtc) {
      return true;
    }
    return new Date(expiresAtUtc).getTime() <= Date.now();
  }

  clear(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    sessionStorage.removeItem(EXPIRES_AT_KEY);
  }
}
