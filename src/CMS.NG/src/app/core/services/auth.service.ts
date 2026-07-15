import { Injectable } from '@angular/core';

/**
 * Minimal session seam. Holds the auth token (persisted in localStorage) and can clear it.
 * A full Lab 05 login flow can build on this; the error interceptor calls
 * {@link AuthService.clearSession} on a 401.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenKey = 'cms.authToken';

  /** The current auth token, or null when unauthenticated. */
  get token(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  /** Whether there is a stored session token. */
  isAuthenticated(): boolean {
    return this.token !== null;
  }

  /** Stores the auth token (used by the login flow). */
  setToken(token: string): void {
    localStorage.setItem(this.tokenKey, token);
  }

  /** Clears the session — called on logout and on a 401 response. */
  clearSession(): void {
    localStorage.removeItem(this.tokenKey);
  }
}
