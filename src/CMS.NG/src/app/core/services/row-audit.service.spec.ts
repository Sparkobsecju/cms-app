import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { RowAuditService } from './row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

describe('RowAuditService', () => {
  let service: RowAuditService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/rowaudit`;

  const sample: RowAuditEntry = {
    dateTime: '2026-06-04T14:30:00',
    userName: 'alice',
    actionType: 'Update',
    actionDesc: 'Title',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [RowAuditService, provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    service = TestBed.inject(RowAuditService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('history() GETs the trail filtered by tableName + pkid', () => {
    service.history('Course', 123).subscribe((rows) => expect(rows.length).toBe(1));

    const req = http.expectOne((r) => r.url === base);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('tableName')).toBe('Course');
    expect(req.request.params.get('pkid')).toBe('123');
    req.flush([sample]);
  });
});
