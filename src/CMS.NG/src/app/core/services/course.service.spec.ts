import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { CourseService } from './course.service';
import { Course, CourseRequest } from '@core/models/course.model';

describe('CourseService', () => {
  let service: CourseService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/courses`;

  const sample: Course = {
    pkid: 1,
    title: 'Azure 基礎',
    officialTitle: null,
    courseId: 'C001',
    prodCourseId: 'P001',
    friendlyUrl: 'azure-basics',
    displayOrder: 1,
    partnerPkid: 1,
    courseGroupPkid: null,
    publishStatusPkid: 1,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 8,
    listPrice: 12000,
    learningCredit: 3.5,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partnerName: 'Microsoft',
    courseGroupDescription: null,
    publishStatusDescription: '已發布',
    certificationPkids: [],
    jobCategoryPkids: [],
  };

  const request: CourseRequest = {
    pkid: 0,
    title: 'Azure 基礎',
    officialTitle: null,
    courseId: 'C001',
    prodCourseId: 'P001',
    friendlyUrl: 'azure-basics',
    displayOrder: 1,
    partnerPkid: 1,
    courseGroupPkid: null,
    publishStatusPkid: 1,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 8,
    listPrice: 12000,
    learningCredit: 3.5,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    certificationPkids: [1, 2],
    jobCategoryPkids: [3],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CourseService, provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    service = TestBed.inject(CourseService);
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
    service.query({ partnerPkid: 1 }).subscribe((rows) => expect(rows.length).toBe(1));
    const req = http.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ partnerPkid: 1 });
    req.flush([sample]);
  });

  it('get() GETs a single course by numeric pkid', () => {
    service.get(1).subscribe((c) => expect(c.pkid).toBe(1));
    const req = http.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create() POSTs to the collection', () => {
    service.create(request).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, pkid: 5 });
  });

  it('update() PUTs to the collection (pkid in body)', () => {
    const edit: CourseRequest = { ...request, pkid: 1 };
    service.update(edit).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(edit);
    req.flush(sample);
  });

  it('delete() DELETEs by numeric pkid', () => {
    service.delete(1).subscribe();
    const req = http.expectOne(`${base}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
