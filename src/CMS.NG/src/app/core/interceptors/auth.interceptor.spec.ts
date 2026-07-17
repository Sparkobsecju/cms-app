import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '@core/services/auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  // A real API call targets the configured API origin (cross-origin to the SPA — hence CORS).
  const apiUrl = `${environment.apiBaseUrl}/things`;

  function configure(token: string | null): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { token } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock.verify());

  it('attaches the access token as a Bearer Authorization header on API-origin requests', () => {
    configure('abc.def.ghi');

    http.get(apiUrl).subscribe();

    const req = httpMock.expectOne(apiUrl);
    expect(req.request.headers.get('Authorization')).toBe('Bearer abc.def.ghi');
    req.flush([]);
  });

  it('leaves the request unchanged when there is no token', () => {
    configure(null);

    http.get(apiUrl).subscribe();

    const req = httpMock.expectOne(apiUrl);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });

  it('does NOT attach the token to a cross-origin (third-party) request', () => {
    configure('abc.def.ghi');

    const foreignUrl = 'https://evil.example.com/collect';
    http.get(foreignUrl).subscribe();

    const req = httpMock.expectOne(foreignUrl);
    // The bearer token must never leak to a host that isn't our API.
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });
});
