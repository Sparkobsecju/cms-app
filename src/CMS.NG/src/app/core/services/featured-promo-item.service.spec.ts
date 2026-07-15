import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { FeaturedPromoItemService } from './featured-promo-item.service';
import { FeaturedPromoItem, FeaturedPromoItemRequest } from '@core/models/featured-promo-item.model';

describe('FeaturedPromoItemService', () => {
  let service: FeaturedPromoItemService;
  let http: HttpTestingController;
  const base = `${environment.apiBaseUrl}/featured-promo-items`;

  const sample: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 101,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
    promoCode: '20251204_SkillTrainAI',
    trainingCenterName: '台北',
  };

  const request: FeaturedPromoItemRequest = {
    pkid: 0,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 101,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FeaturedPromoItemService, provideHttpClient(withFetch()), provideHttpClientTesting()],
    });
    service = TestBed.inject(FeaturedPromoItemService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() GETs the collection', () => {
    service.list().subscribe((rows) => expect(rows.length).toBe(1));
    const req = http.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);
  });

  it('query() POSTs a one-week + center filter to /query', () => {
    const filter = { trainingCenterPkid: 1, scheduleOnFrom: '2026-03-16', scheduleOnTo: '2026-03-22' };
    service.query(filter).subscribe((rows) => expect(rows.length).toBe(1));
    const req = http.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([sample]);
  });

  it('get() GETs a single item by numeric pkid', () => {
    service.get(1).subscribe((i) => expect(i.pkid).toBe(1));
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
    const edit: FeaturedPromoItemRequest = { ...request, pkid: 1 };
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

  it('moveSlot() POSTs to /{id}/move/{direction}', () => {
    service.moveSlot(1, 'down').subscribe();
    const req = http.expectOne(`${base}/1/move/down`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });
});
