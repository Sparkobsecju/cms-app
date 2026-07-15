import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { PartnerService } from './partner.service';
import { Partner, PartnerRequest } from '@core/models/partner.model';

describe('PartnerService', () => {
  let service: PartnerService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/partners`;

  const sample: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 選單',
    nameOnCourseDetailPage: 'Microsoft',
    displayOrder: 1,
    imageFilename: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PartnerService, provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    service = TestBed.inject(PartnerService);
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
    service.query({ keyword: 'Micro' }).subscribe((rows) => expect(rows.length).toBe(1));
    const req = http.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'Micro' });
    req.flush([sample]);
  });

  it('get() GETs a single partner by numeric pkid', () => {
    service.get(1).subscribe((p) => expect(p.pkid).toBe(1));
    const req = http.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create() POSTs to the collection', () => {
    const request: PartnerRequest = {
      pkid: 0,
      name: 'Amazon',
      appKey: 'AWS',
      nameOnPartnerMenu: 'Amazon 選單',
      nameOnCourseDetailPage: 'Amazon',
      displayOrder: 2,
      imageFilename: null,
    };
    service.create(request).subscribe();
    const req = http.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, pkid: 5, name: 'Amazon' });
  });

  it('update() PUTs to the collection (pkid in body)', () => {
    const request: PartnerRequest = {
      pkid: 1,
      name: 'Microsoft',
      appKey: 'MS',
      nameOnPartnerMenu: 'Microsoft 選單',
      nameOnCourseDetailPage: 'Microsoft',
      displayOrder: 1,
      imageFilename: null,
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
