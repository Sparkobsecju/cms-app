import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { AppUserService } from './app-user.service';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';

describe('AppUserService', () => {
  let service: AppUserService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/appusers`;

  const sample: AppUser = {
    pkid: 1, userId: 'helen', userName: 'Helen', isActive: true,
    passwordUpdatedTime: null, roleCount: 2, roleIds: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AppUserService, provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    service = TestBed.inject(AppUserService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() GETs the collection', () => {
    service.list().subscribe((rows) => expect(rows.length).toBe(1));
    const req = http.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);
  });

  it('query() POSTs the filter to /query', () => {
    service.query({ keyword: 'hel', isActive: true }).subscribe((rows) => expect(rows.length).toBe(1));
    const req = http.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'hel', isActive: true });
    req.flush([sample]);
  });

  it('get() GETs a single user and encodes the id', () => {
    service.get('miles@uuu.com.tw').subscribe((u) => expect(u.userId).toBe('helen'));
    const req = http.expectOne(`${base}/miles%40uuu.com.tw`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create() POSTs to the collection', () => {
    const request: AppUserRequest = { userId: 'newbie', userName: 'Newbie', isActive: true, roleIds: [] };
    service.create(request).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, userId: 'newbie' });
  });

  it('update() PUTs to the collection (id in body)', () => {
    const request: AppUserRequest = { userId: 'helen', userName: 'Helen', isActive: true, roleIds: [] };
    service.update(request).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(sample);
  });

  it('delete() DELETEs by encoded id', () => {
    service.delete('helen').subscribe();
    const req = http.expectOne(`${base}/helen`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('resetPassword() POSTs to /{id}/reset-password with an empty body', () => {
    service.resetPassword('miles@uuu.com.tw').subscribe();
    const req = http.expectOne(`${base}/miles%40uuu.com.tw/reset-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(null);
  });
});
