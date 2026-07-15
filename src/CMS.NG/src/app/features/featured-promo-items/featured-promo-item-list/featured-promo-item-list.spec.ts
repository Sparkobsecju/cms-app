import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { FeaturedPromoItemList } from './featured-promo-item-list';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import { FeaturedPromoItem, FeaturedPromoItemRequest } from '@core/models/featured-promo-item.model';

function item(pkid: number, iso: string, slot: number): FeaturedPromoItem {
  return {
    pkid, scheduleOn: iso, trainingCenterPkid: 1, slot, promotionPkid: 100 + pkid,
    topic: `主題 ${pkid}`, description: `說明 ${pkid}`, promoCode: `CODE_${pkid}`, trainingCenterName: '台北',
  };
}

describe('FeaturedPromoItemList', () => {
  let fixture: ComponentFixture<FeaturedPromoItemList>;
  let component: FeaturedPromoItemList;
  let service: jasmine.SpyObj<FeaturedPromoItemService>;

  const rows: FeaturedPromoItem[] = [item(1, '2026-03-16', 1)];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<FeaturedPromoItemService>('FeaturedPromoItemService',
      ['query', 'create', 'update', 'delete', 'moveSlot']);
    service.query.and.returnValue(of(rows));
    service.create.and.returnValue(of(rows[0]));
    service.update.and.returnValue(of(rows[0]));
    service.delete.and.returnValue(of(void 0));
    service.moveSlot.and.returnValue(of(void 0));

    const lookups = jasmine.createSpyObj<LookupService>('LookupService', ['trainingCenters', 'promotions']);
    lookups.trainingCenters.and.returnValue(of([{ pkid: 1, name: '台北' }, { pkid: 2, name: '新竹' }]));
    lookups.promotions.and.returnValue(of([
      { pkid: 101, promoCode: 'CODE_1', topic: '主題 1', description: '說明 1' },
    ]));

    await TestBed.configureTestingModule({
      imports: [FeaturedPromoItemList],
      providers: [
        provideNoopAnimations(),
        ConfirmationService,
        MessageService,
        { provide: FeaturedPromoItemService, useValue: service },
        { provide: LookupService, useValue: lookups },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FeaturedPromoItemList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads centers and queries the active center on init', () => {
    expect(component['activeCenterPkid']()).toBe(1);
    expect(service.query).toHaveBeenCalled();
    expect(component['items']().length).toBe(1);
  });

  it('renders a tab per training center', () => {
    const tabs = fixture.nativeElement.querySelectorAll('.center-tab');
    expect(tabs.length).toBe(2);
  });

  it('switches center and re-queries', () => {
    component['selectCenter'](2);
    expect(component['activeCenterPkid']()).toBe(2);
    const arg = service.query.calls.mostRecent().args[0];
    expect(arg.trainingCenterPkid).toBe(2);
  });

  it('sends a one-week (Mon–Sun) range in the query', () => {
    const arg = service.query.calls.mostRecent().args[0];
    // 7-day inclusive window: from + 6 days === to.
    const from = new Date(`${arg.scheduleOnFrom}T00:00:00`);
    const to = new Date(`${arg.scheduleOnTo}T00:00:00`);
    const days = Math.round((to.getTime() - from.getTime()) / 86_400_000);
    expect(days).toBe(6);
    expect(from.getDay()).toBe(1); // Monday
  });

  it('advances the week and re-queries', () => {
    const before = service.query.calls.count();
    component['nextWeek']();
    expect(service.query.calls.count()).toBe(before + 1);
  });

  it('finds an item by day + slot', () => {
    expect(component['itemAt']('2026-03-16', 1)?.pkid).toBe(1);
    expect(component['itemAt']('2026-03-16', 2)).toBeNull();
  });

  it('copies a row then opens a paste form on an empty slot', () => {
    component['copy'](rows[0]);
    expect(component['clipboard']()?.promoCode).toBe('CODE_1');
    component['startPaste']('2026-03-17', 2);
    const editing = component['editing']();
    expect(editing?.item).toBeNull();
    expect(editing?.seed?.promoCode).toBe('CODE_1');
  });

  it('creates via the service when saved with pkid 0', () => {
    const request: FeaturedPromoItemRequest = {
      pkid: 0, scheduleOn: '2026-03-16', trainingCenterPkid: 1, slot: 3,
      promotionPkid: 101, topic: 't', description: 'd',
    };
    component['onSaved'](request);
    expect(service.create).toHaveBeenCalledWith(request);
  });

  it('updates via the service when saved with an existing pkid', () => {
    const request: FeaturedPromoItemRequest = {
      pkid: 9, scheduleOn: '2026-03-16', trainingCenterPkid: 1, slot: 1,
      promotionPkid: 101, topic: 't', description: 'd',
    };
    component['onSaved'](request);
    expect(service.update).toHaveBeenCalledWith(request);
  });

  it('moves a slot through the service', () => {
    component['move'](rows[0], 'down');
    expect(service.moveSlot).toHaveBeenCalledWith(1, 'down');
  });

  it('deletes through the service on confirm', () => {
    const confirm = TestBed.inject(ConfirmationService);
    spyOn(confirm, 'confirm').and.callFake((opts) => {
      opts.accept?.();
      return confirm;
    });
    component['confirmDelete'](rows[0]);
    expect(service.delete).toHaveBeenCalledWith(1);
  });
});
