import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { errorInterceptor } from './error.interceptor';
import { AuthService } from '@core/services/auth.service';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let messages: jasmine.SpyObj<MessageService>;
  let auth: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    messages = jasmine.createSpyObj<MessageService>('MessageService', ['add']);
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['clearSession']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withFetch(), withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: MessageService, useValue: messages },
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('shows a friendly toast on a 500 using the safe message from the body', () => {
    http.get('/api/boom').subscribe({ next: () => fail('should have errored'), error: () => {} });

    httpMock.expectOne('/api/boom').flush(
      { message: 'An unexpected error occurred.' },
      { status: 500, statusText: 'Internal Server Error' },
    );

    expect(messages.add).toHaveBeenCalledTimes(1);
    const arg = messages.add.calls.mostRecent().args[0];
    expect(arg.severity).toBe('error');
    expect(arg.detail).toBe('An unexpected error occurred.');
    expect(auth.clearSession).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('clears the session and redirects to Login on a 401', () => {
    http.get('/api/secure').subscribe({ next: () => fail('should have errored'), error: () => {} });

    httpMock.expectOne('/api/secure').flush(
      { message: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(auth.clearSession).toHaveBeenCalledTimes(1);
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(messages.add).not.toHaveBeenCalled();
  });

  it('passes a 400 validation error through without a toast or redirect', () => {
    http.post('/api/coursegroups', {}).subscribe({ next: () => fail('should have errored'), error: () => {} });

    httpMock.expectOne('/api/coursegroups').flush(
      'Description is required.',
      { status: 400, statusText: 'Bad Request' },
    );

    expect(messages.add).not.toHaveBeenCalled();
    expect(auth.clearSession).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});

// A 401 must actually empty session storage — exercised end-to-end with the REAL AuthService.
describe('errorInterceptor + real AuthService (session clearing)', () => {
  const SESSION_KEY = 'cms.session';
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    sessionStorage.setItem(
      SESSION_KEY,
      JSON.stringify({ userId: 'a', userName: 'A', accessToken: 'x.y.z' }),
    );
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withFetch(), withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        AuthService, // real service — clearSession() must remove the stored session
        { provide: MessageService, useValue: jasmine.createSpyObj<MessageService>('MessageService', ['add']) },
        { provide: Router, useValue: router },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('empties session storage and navigates to /login on a 401', () => {
    http.get('/api/secure').subscribe({ next: () => fail('should have errored'), error: () => {} });

    httpMock.expectOne('/api/secure').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem(SESSION_KEY)).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});
