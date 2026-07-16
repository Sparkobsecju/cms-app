import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '@env/environment';

/** The signed-in profile returned by the login endpoint and kept in session storage. */
export interface AuthProfile {
  userId: string;
  userName: string;
  accessToken: string;
}

/** The identity fields returned by the profile-update endpoint (no token). */
export interface ProfileUpdate {
  userId: string;
  userName: string;
}

/** Session-storage key holding the JSON-serialised {@link AuthProfile}. */
const SESSION_KEY = 'cms.session';

/**
 * Session seam for the JWT login flow. Persists the profile (userId, userName, accessToken) in
 * <b>session</b> storage (cleared when the tab closes), exposes the signed-in name and the roles
 * decoded from the token, and clears everything on logout / a 401.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly loginUrl = `${environment.apiBaseUrl}/Auth/login`;
  private readonly profileUrl = `${environment.apiBaseUrl}/Auth/profile`;
  private readonly changePasswordUrl = `${environment.apiBaseUrl}/Auth/change-password`;

  private readonly _profile = signal<AuthProfile | null>(readSession());

  /** The signed-in profile, or null when unauthenticated. */
  readonly profile = this._profile.asReadonly();

  /** The signed-in display name (empty when signed out). */
  readonly userName = computed(() => this._profile()?.userName ?? '');

  /** Roles decoded from the access token (empty when signed out). */
  readonly roles = computed(() => decodeRoles(this._profile()?.accessToken));

  /** The current access token, or null when unauthenticated. */
  get token(): string | null {
    return this._profile()?.accessToken ?? null;
  }

  /** Whether there is a stored session. */
  isAuthenticated(): boolean {
    return this._profile() !== null;
  }

  /** Whether the signed-in user holds the given role (read from the token, no extra API call). */
  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  /** POST credentials to the login endpoint and, on success, store the returned profile. */
  login(userId: string, password: string): Observable<AuthProfile> {
    return this.http
      .post<AuthProfile>(this.loginUrl, { userId, password })
      .pipe(tap((profile) => this.setSession(profile)));
  }

  /**
   * Updates the signed-in user's own display name via <c>PUT /api/Auth/profile</c>. The backend takes
   * the target UserId from the JWT, so only the new name is sent. On success the stored profile (and thus
   * the shell's userName + session storage) is refreshed with the returned name; the access token is kept.
   */
  updateUserName(userName: string): Observable<ProfileUpdate> {
    return this.http.put<ProfileUpdate>(this.profileUrl, { userName }).pipe(
      tap((updated) => {
        const current = this._profile();
        if (current) {
          this.setSession({ ...current, userName: updated.userName });
        }
      }),
    );
  }

  /**
   * Changes the signed-in user's own password via <c>POST /api/Auth/change-password</c>. Only plaintext
   * passwords are sent — never a hash — and the backend takes the target UserId from the JWT. The current
   * session (token included) is left untouched on success; the caller surfaces the result to the user.
   */
  changePassword(currentPassword: string, newPassword: string, confirmPassword: string): Observable<void> {
    return this.http.post<void>(this.changePasswordUrl, { currentPassword, newPassword, confirmPassword });
  }

  /** Persists the session profile in session storage. */
  setSession(profile: AuthProfile): void {
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(profile));
    this._profile.set(profile);
  }

  /** Clears the session — called on logout and on a 401 response. */
  clearSession(): void {
    sessionStorage.removeItem(SESSION_KEY);
    this._profile.set(null);
  }
}

/** Reads and validates the stored session profile, or null when absent/corrupt. */
function readSession(): AuthProfile | null {
  const raw = sessionStorage.getItem(SESSION_KEY);
  if (!raw) {
    return null;
  }
  try {
    const parsed = JSON.parse(raw) as Partial<AuthProfile>;
    if (parsed && typeof parsed.accessToken === 'string' && parsed.accessToken) {
      return {
        userId: parsed.userId ?? '',
        userName: parsed.userName ?? '',
        accessToken: parsed.accessToken,
      };
    }
  } catch {
    // Corrupt session — treat as signed out.
  }
  return null;
}

/**
 * Extracts role names from a JWT. The backend emits them under the .NET role claim URI; short
 * "role"/"roles" keys are also accepted for robustness. Returns [] for a missing/invalid token.
 */
function decodeRoles(token: string | null | undefined): string[] {
  const payload = token ? decodeJwtPayload(token) : null;
  if (!payload) {
    return [];
  }
  const roleUri = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
  const raw = payload[roleUri] ?? payload['role'] ?? payload['roles'];
  if (Array.isArray(raw)) {
    return raw.filter((r): r is string => typeof r === 'string');
  }
  return typeof raw === 'string' ? [raw] : [];
}

/** Base64url-decodes the JWT payload segment to a claims object, or null if it can't be parsed. */
function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const segment = token.split('.')[1];
  if (!segment) {
    return null;
  }
  try {
    const base64 = segment.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    return JSON.parse(atob(padded)) as Record<string, unknown>;
  } catch {
    return null;
  }
}
