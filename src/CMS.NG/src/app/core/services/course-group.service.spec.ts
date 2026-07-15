import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { CourseGroupService } from './course-group.service';
import { CourseGroup, CourseGroupRequest } from '@core/models/course-group.model';

describe('CourseGroupService', () => {
  let service: CourseGroupService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/coursegroups`;

  const sample: CourseGroup = { pkid: 1, description: '資訊技術' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CourseGroupService, provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    service = TestBed.inject(CourseGroupService);
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
    service.query({ keyword: '資訊' }).subscribe((rows) => expect(rows.length).toBe(1));
    const req = http.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: '資訊' });
    req.flush([sample]);
  });

  it('get() GETs a single group by numeric pkid', () => {
    service.get(1).subscribe((g) => expect(g.pkid).toBe(1));
    const req = http.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create() POSTs to the collection', () => {
    const request: CourseGroupRequest = { pkid: 0, description: '數位轉型' };
    service.create(request).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, pkid: 5, description: '數位轉型' });
  });

  it('update() PUTs to the collection (pkid in body)', () => {
    const request: CourseGroupRequest = { pkid: 1, description: '資訊技術' };
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
