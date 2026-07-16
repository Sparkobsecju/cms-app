import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { CourseGroupList } from './course-group-list';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';

describe('CourseGroupList', () => {
  let fixture: ComponentFixture<CourseGroupList>;
  let component: CourseGroupList;
  let service: jasmine.SpyObj<CourseGroupService>;
  let router: Router;

  const rows: CourseGroup[] = [
    { pkid: 2, description: '在職進修' },
    { pkid: 1, description: '資訊技術' },
  ];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['list', 'query', 'delete']);
    service.list.and.returnValue(of(rows));
    service.query.and.returnValue(of([rows[1]]));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [CourseGroupList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        ConfirmationService,
        MessageService,
        { provide: CourseGroupService, useValue: service },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('loads all groups on init', () => {
    expect(service.list).toHaveBeenCalled();
    expect(component['groups']().length).toBe(2);
  });

  it('renders a row per group', () => {
    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(bodyRows.length).toBe(2);
  });

  it('applies a keyword filter via query()', () => {
    component['filter'] = { keyword: '資訊' };
    component['applyFilter']();
    expect(service.query).toHaveBeenCalledWith({ keyword: '資訊' });
    expect(component['groups']().length).toBe(1);
  });

  it('navigates to the add page', () => {
    const nav = spyOn(router, 'navigate');
    component['add']();
    expect(nav).toHaveBeenCalledWith(['/course-groups/new']);
  });

  it('deletes through the service on confirm', () => {
    const confirm = TestBed.inject(ConfirmationService);
    spyOn(confirm, 'confirm').and.callFake((opts) => {
      opts.accept?.();
      return confirm;
    });
    component['confirmDelete'](rows[1]);
    expect(service.delete).toHaveBeenCalledWith(1);
  });
});
