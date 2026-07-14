import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { AppRoleService } from './app-role.service';
import { AppRole, AppRoleRequest } from '@core/models/app-role.model';

describe('AppRoleService', () => {
  let service: AppRoleService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/approles`;

  const sample: AppRole = {
    pkid: 1, roleId: 'Admin', roleName: 'Administrator',
    permissionLevel: 1, description: '系統管理員', userCount: 3, userIds: [],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AppRoleService, provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    service = TestBed.inject(AppRoleService);
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
    service.query({ keyword: 'adm', permissionLevel: 1 }).subscribe((rows) => expect(rows.length).toBe(1));
    const req = http.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'adm', permissionLevel: 1 });
    req.flush([sample]);
  });

  it('get() GETs a single role and encodes the id', () => {
    service.get('miles@uuu.com.tw').subscribe((r) => expect(r.roleId).toBe('Admin'));
    const req = http.expectOne(`${base}/miles%40uuu.com.tw`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create() POSTs to the collection', () => {
    const request: AppRoleRequest = { roleId: 'Editor', roleName: 'Editor', permissionLevel: 50, userIds: [] };
    service.create(request).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, roleId: 'Editor' });
  });

  it('update() PUTs to the collection (id in body)', () => {
    const request: AppRoleRequest = { roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1, userIds: [] };
    service.update(request).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(sample);
  });

  it('delete() DELETEs by encoded id', () => {
    service.delete('Admin').subscribe();
    const req = http.expectOne(`${base}/Admin`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
