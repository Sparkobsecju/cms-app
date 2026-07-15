import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { PartnerList } from './partner-list';
import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';

describe('PartnerList', () => {
  let fixture: ComponentFixture<PartnerList>;
  let component: PartnerList;
  let service: jasmine.SpyObj<PartnerService>;
  let router: Router;

  const rows: Partner[] = [
    { pkid: 1, name: 'Microsoft', appKey: 'MS', nameOnPartnerMenu: 'MS 選單', nameOnCourseDetailPage: 'Microsoft', displayOrder: 1, imageFilename: null },
    { pkid: 2, name: 'Cisco', appKey: 'CSCO', nameOnPartnerMenu: 'Cisco 選單', nameOnCourseDetailPage: 'Cisco', displayOrder: 2, imageFilename: null },
  ];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['list', 'query', 'delete']);
    service.list.and.returnValue(of(rows));
    service.query.and.returnValue(of([rows[1]]));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PartnerList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        ConfirmationService,
        MessageService,
        { provide: PartnerService, useValue: service },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('loads all partners on init', () => {
    expect(service.list).toHaveBeenCalled();
    expect(component['partners']().length).toBe(2);
  });

  it('renders a row per partner', () => {
    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(bodyRows.length).toBe(2);
  });

  it('applies a keyword filter via query()', () => {
    component['filter'] = { keyword: 'Cisco' };
    component['applyFilter']();
    expect(service.query).toHaveBeenCalledWith({ keyword: 'Cisco' });
    expect(component['partners']().length).toBe(1);
  });

  it('navigates to the add page', () => {
    const nav = spyOn(router, 'navigate');
    component['add']();
    expect(nav).toHaveBeenCalledWith(['/partners/new']);
  });

  it('navigates to courses filtered by partner', () => {
    const nav = spyOn(router, 'navigate');
    component['viewCourses'](rows[0]);
    expect(nav).toHaveBeenCalledWith(['/courses'], { queryParams: { partnerPkid: 1 } });
  });

  it('deletes through the service on confirm', () => {
    const confirm = TestBed.inject(ConfirmationService);
    spyOn(confirm, 'confirm').and.callFake((opts) => {
      opts.accept?.();
      return confirm;
    });
    component['confirmDelete'](rows[1]);
    expect(service.delete).toHaveBeenCalledWith(2);
  });
});
