import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { PublishStatusService } from './publish-status.service';
import { PublishStatus, PublishStatusRequest } from '@core/models/publish-status.model';

describe('PublishStatusService', () => {
  let service: PublishStatusService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/publishstatuses`;

  const sample: PublishStatus = {
    pkid: 1, description: 'Draft', isDraft: true, isPublished: false, isDiscontinued: false,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PublishStatusService, provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    service = TestBed.inject(PublishStatusService);
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
    service.query({ keyword: 'pub', isPublished: true }).subscribe((rows) => expect(rows.length).toBe(1));
    const req = http.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'pub', isPublished: true });
    req.flush([sample]);
  });

  it('get() GETs a single status by numeric pkid', () => {
    service.get(1).subscribe((s) => expect(s.pkid).toBe(1));
    const req = http.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create() POSTs to the collection', () => {
    const request: PublishStatusRequest = {
      pkid: 3, description: 'Archived', isDraft: false, isPublished: false, isDiscontinued: true,
    };
    service.create(request).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, pkid: 3, description: 'Archived' });
  });

  it('update() PUTs to the collection (pkid in body)', () => {
    const request: PublishStatusRequest = {
      pkid: 1, description: 'Draft', isDraft: true, isPublished: false, isDiscontinued: false,
    };
    service.update(request).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(sample);
  });

  it('delete() DELETEs by numeric pkid', () => {
    service.delete(1).subscribe();
    const req = http.expectOne(`${base}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
