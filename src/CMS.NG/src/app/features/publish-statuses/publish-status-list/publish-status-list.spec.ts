import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { PublishStatusList } from './publish-status-list';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';

describe('PublishStatusList', () => {
  let fixture: ComponentFixture<PublishStatusList>;
  let component: PublishStatusList;
  let service: jasmine.SpyObj<PublishStatusService>;
  let router: Router;

  const rows: PublishStatus[] = [
    { pkid: 1, description: 'Draft', isDraft: true, isPublished: false, isDiscontinued: false },
    { pkid: 2, description: 'Published', isDraft: false, isPublished: true, isDiscontinued: false },
  ];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['list', 'query', 'delete']);
    service.list.and.returnValue(of(rows));
    service.query.and.returnValue(of([rows[1]]));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PublishStatusList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        ConfirmationService,
        MessageService,
        { provide: PublishStatusService, useValue: service },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('loads all statuses on init', () => {
    expect(service.list).toHaveBeenCalled();
    expect(component['statuses']().length).toBe(2);
  });

  it('renders a row per status', () => {
    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(bodyRows.length).toBe(2);
  });

  it('applies a tri-state filter via query()', () => {
    component['filter'] = { keyword: null, isDraft: null, isPublished: true, isDiscontinued: null };
    component['applyFilter']();
    expect(service.query).toHaveBeenCalledWith({ keyword: null, isDraft: null, isPublished: true, isDiscontinued: null });
    expect(component['statuses']().length).toBe(1);
  });

  it('navigates to the add page', () => {
    const nav = spyOn(router, 'navigate');
    component['add']();
    expect(nav).toHaveBeenCalledWith(['/publish-statuses/new']);
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
