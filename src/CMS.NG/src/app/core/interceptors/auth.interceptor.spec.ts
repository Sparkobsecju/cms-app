import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '@core/services/auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

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

  it('attaches the access token as a Bearer Authorization header when signed in', () => {
    configure('abc.def.ghi');

    http.get('/api/things').subscribe();

    const req = httpMock.expectOne('/api/things');
    expect(req.request.headers.get('Authorization')).toBe('Bearer abc.def.ghi');
    req.flush([]);
  });

  it('leaves the request unchanged when there is no token', () => {
    configure(null);

    http.get('/api/things').subscribe();

    const req = httpMock.expectOne('/api/things');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });
});
