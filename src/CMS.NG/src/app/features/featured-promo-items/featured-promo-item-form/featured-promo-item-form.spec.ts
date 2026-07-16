import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { FeaturedPromoItemForm } from './featured-promo-item-form';
import { FeaturedPromoItem, PromotionLookup } from '@core/models/featured-promo-item.model';

const promotions: PromotionLookup[] = [
  { pkid: 101, promoCode: '20251204_SkillTrainAI', topic: '成為能AI協作的程式設計師', description: '轉職就業養成班' },
  { pkid: 102, promoCode: '251211_GoogleAI', topic: 'Google AI工具一次掌握', description: '不需技術基礎' },
];

const existing: FeaturedPromoItem = {
  pkid: 7,
  scheduleOn: '2026-03-16',
  trainingCenterPkid: 1,
  slot: 2,
  promotionPkid: 102,
  topic: 'Google AI工具一次掌握',
  description: '不需技術基礎',
  promoCode: '251211_GoogleAI',
  trainingCenterName: '台北',
};

function setup(item: FeaturedPromoItem | null) {
  TestBed.configureTestingModule({
    imports: [FeaturedPromoItemForm],
    providers: [provideNoopAnimations(), MessageService],
  });
  const fixture: ComponentFixture<FeaturedPromoItemForm> = TestBed.createComponent(FeaturedPromoItemForm);
  const component = fixture.componentInstance;
  component.item = item;
  component.promotions = promotions;
  component.scheduleOn = '2026-03-16';
  component.trainingCenterPkid = 1;
  component.slot = item?.slot ?? 1;
  fixture.detectChanges(); // triggers ngOnInit
  return { fixture, component };
}

describe('FeaturedPromoItemForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('starts blank in new mode', () => {
    const { component } = setup(null);
    expect(component['form'].getRawValue().promoCode).toBe('');
    expect(component['promotionPkid']()).toBeNull();
  });

  it('patches the form and resolves promotionPkid in edit mode', () => {
    const { component } = setup(existing);
    expect(component['form'].getRawValue().promoCode).toBe('251211_GoogleAI');
    expect(component['promotionPkid']()).toBe(102);
  });

  it('resolves an entered PromoCode to its pkid and pre-fills topic/description', () => {
    const { component } = setup(null);
    component['form'].controls.promoCode.setValue('20251204_SkillTrainAI');
    component['lookupPromoCode']();
    expect(component['promotionPkid']()).toBe(101);
    expect(component['form'].getRawValue().topic).toBe('成為能AI協作的程式設計師');
    expect(component['form'].getRawValue().description).toBe('轉職就業養成班');
  });

  it('does not resolve an unknown PromoCode', () => {
    const { component } = setup(null);
    component['form'].controls.promoCode.setValue('NOPE');
    component['lookupPromoCode']();
    expect(component['promotionPkid']()).toBeNull();
  });

  it('emits a create request (pkid 0) on submit in new mode', () => {
    const { component } = setup(null);
    const emitted = jasmine.createSpy('saved');
    component.saved.subscribe(emitted);
    component['form'].controls.promoCode.setValue('20251204_SkillTrainAI');
    component['lookupPromoCode']();
    component['submit']();
    expect(emitted).toHaveBeenCalled();
    const req = emitted.calls.mostRecent().args[0];
    expect(req.pkid).toBe(0);
    expect(req.promotionPkid).toBe(101);
    expect(req.scheduleOn).toBe('2026-03-16');
    expect(req.slot).toBe(1);
  });

  it('emits an update request with the existing pkid in edit mode', () => {
    const { component } = setup(existing);
    const emitted = jasmine.createSpy('saved');
    component.saved.subscribe(emitted);
    component['submit']();
    expect(emitted).toHaveBeenCalled();
    expect(emitted.calls.mostRecent().args[0].pkid).toBe(7);
  });

  it('blocks submit until a PromoCode is resolved', () => {
    const { component } = setup(null);
    const emitted = jasmine.createSpy('saved');
    component.saved.subscribe(emitted);
    component['form'].controls.promoCode.setValue('unresolved');
    component['submit']();
    expect(emitted).not.toHaveBeenCalled();
  });
});
