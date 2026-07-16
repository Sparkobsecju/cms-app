import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { AuthService } from './auth.service';

const ROLE_URI = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const SESSION_KEY = 'cms.session';

/** Builds a JWT-shaped token (header.payload.signature) with a base64url payload. */
function makeJwt(payload: Record<string, unknown>): string {
  const enc = (obj: unknown) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${enc({ alg: 'HS256', typ: 'JWT' })}.${enc(payload)}.signature`;
}

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('starts unauthenticated when session storage is empty', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.token).toBeNull();
    expect(service.roles()).toEqual([]);
    expect(service.userName()).toBe('');
  });

  it('login POSTs the credentials and stores the returned profile in session storage', () => {
    const token = makeJwt({ UserName: 'Helen', [ROLE_URI]: ['Admin'] });
    let emitted: unknown;

    service.login('helen', 'secret').subscribe((p) => (emitted = p));

    const req = http.expectOne(`${environment.apiBaseUrl}/Auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'helen', password: 'secret' });
    req.flush({ userId: 'helen', userName: 'Helen', accessToken: token });

    expect(emitted).toEqual({ userId: 'helen', userName: 'Helen', accessToken: token });
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.token).toBe(token);
    expect(service.userName()).toBe('Helen');

    const stored = JSON.parse(sessionStorage.getItem(SESSION_KEY)!);
    expect(stored).toEqual({ userId: 'helen', userName: 'Helen', accessToken: token });
  });

  it('decodes the roles carried in the access token', () => {
    const token = makeJwt({ [ROLE_URI]: ['Admin', 'Editor'] });
    service.setSession({ userId: 'a', userName: 'A', accessToken: token });

    expect(service.roles()).toEqual(['Admin', 'Editor']);
    expect(service.hasRole('Admin')).toBeTrue();
    expect(service.hasRole('Viewer')).toBeFalse();
  });

  it('treats a single string role claim as one role', () => {
    const token = makeJwt({ [ROLE_URI]: 'Admin' });
    service.setSession({ userId: 'a', userName: 'A', accessToken: token });

    expect(service.roles()).toEqual(['Admin']);
    expect(service.hasRole('Admin')).toBeTrue();
  });

  it('updateUserName PUTs only the name and refreshes the stored profile, keeping the token', () => {
    const token = makeJwt({ UserName: 'Helen', [ROLE_URI]: ['Admin'] });
    service.setSession({ userId: 'helen', userName: 'Helen', accessToken: token });

    service.updateUserName('Helen Wu').subscribe();

    const req = http.expectOne(`${environment.apiBaseUrl}/Auth/profile`);
    expect(req.request.method).toBe('PUT');
    // Only the new name is sent — the UserId is taken from the JWT by the backend.
    expect(req.request.body).toEqual({ userName: 'Helen Wu' });
    req.flush({ userId: 'helen', userName: 'Helen Wu' });

    // Shell name + session storage refresh; the access token is preserved.
    expect(service.userName()).toBe('Helen Wu');
    expect(service.token).toBe(token);
    const stored = JSON.parse(sessionStorage.getItem(SESSION_KEY)!);
    expect(stored).toEqual({ userId: 'helen', userName: 'Helen Wu', accessToken: token });
  });

  it('changePassword POSTs only the plaintext fields (never a hash) and keeps the session', () => {
    const token = makeJwt({ UserName: 'Helen', [ROLE_URI]: ['Admin'] });
    service.setSession({ userId: 'helen', userName: 'Helen', accessToken: token });

    service.changePassword('OldPass1!', 'NewPass9#', 'NewPass9#').subscribe();

    const req = http.expectOne(`${environment.apiBaseUrl}/Auth/change-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      currentPassword: 'OldPass1!',
      newPassword: 'NewPass9#',
      confirmPassword: 'NewPass9#',
    });
    // No password hash is ever sent from the client.
    expect(JSON.stringify(req.request.body)).not.toContain('hash');
    req.flush(null);

    // The session (and token) is untouched by a password change.
    expect(service.token).toBe(token);
    expect(service.userName()).toBe('Helen');
  });

  it('clearSession removes the profile from session storage', () => {
    service.setSession({ userId: 'a', userName: 'A', accessToken: makeJwt({}) });
    expect(sessionStorage.getItem(SESSION_KEY)).not.toBeNull();

    service.clearSession();

    expect(sessionStorage.getItem(SESSION_KEY)).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.roles()).toEqual([]);
  });
});
