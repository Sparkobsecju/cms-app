import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { PartnerDetail } from './partner-detail';
import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';

const partner: Partner = {
  pkid: 1,
  name: 'Microsoft',
  appKey: 'MS',
  nameOnPartnerMenu: 'Microsoft 選單',
  nameOnCourseDetailPage: 'Microsoft',
  displayOrder: 1,
  imageFilename: null,
};

describe('PartnerDetail', () => {
  let fixture: ComponentFixture<PartnerDetail>;
  let component: PartnerDetail;
  let service: jasmine.SpyObj<PartnerService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['get']);
    service.get.and.returnValue(of(partner));

    await TestBed.configureTestingModule({
      imports: [PartnerDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: PartnerService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the partner by numeric id from the route', () => {
    expect(service.get).toHaveBeenCalledWith(1);
    expect(component['partner']()?.name).toBe('Microsoft');
  });

  it('navigates to edit', () => {
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['edit']();
    expect(nav).toHaveBeenCalledWith(['/partners', 1, 'edit']);
  });
});
