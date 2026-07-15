import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { CourseList } from './course-list';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course } from '@core/models/course.model';

function course(pkid: number, title: string): Course {
  return {
    pkid, title, officialTitle: null, courseId: `C${pkid}`, prodCourseId: `P${pkid}`,
    friendlyUrl: `c-${pkid}`, displayOrder: pkid, partnerPkid: 1, courseGroupPkid: null,
    publishStatusPkid: 1, scheduleOn: '2026-01-01', scheduleOff: '2036-01-01', hour: 8,
    listPrice: 12000, learningCredit: 3.5, material: null, objective: null, target: null,
    prerequisites: null, outline: null, towardCertOrExam: null, note: null, otherInfo: null,
    canRepeat: true, partnerName: 'Microsoft', courseGroupDescription: null,
    publishStatusDescription: '已發布', certificationPkids: [], jobCategoryPkids: [],
  };
}

describe('CourseList', () => {
  let fixture: ComponentFixture<CourseList>;
  let component: CourseList;
  let service: jasmine.SpyObj<CourseService>;
  let router: Router;

  const rows: Course[] = [course(1, 'Azure 基礎'), course(2, 'AWS 進階')];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<CourseService>('CourseService', ['list', 'query', 'delete']);
    service.list.and.returnValue(of(rows));
    service.query.and.returnValue(of([rows[0]]));
    service.delete.and.returnValue(of(void 0));

    const lookups = jasmine.createSpyObj<LookupService>('LookupService',
      ['partners', 'courseGroups', 'publishStatuses', 'certifications', 'jobCategories']);
    lookups.partners.and.returnValue(of([]));
    lookups.courseGroups.and.returnValue(of([]));
    lookups.publishStatuses.and.returnValue(of([]));
    lookups.certifications.and.returnValue(of([]));
    lookups.jobCategories.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [CourseList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        ConfirmationService,
        MessageService,
        { provide: CourseService, useValue: service },
        { provide: LookupService, useValue: lookups },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CourseList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('loads all courses on init', () => {
    expect(service.list).toHaveBeenCalled();
    expect(component['courses']().length).toBe(2);
  });

  it('renders a row per course', () => {
    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(bodyRows.length).toBe(2);
  });

  it('applies a partner filter via query()', () => {
    component['filter'] = { partnerPkid: 1 };
    component['applyFilter']();
    expect(service.query).toHaveBeenCalled();
    expect(component['courses']().length).toBe(1);
  });

  it('navigates to the add page', () => {
    const nav = spyOn(router, 'navigate');
    component['add']();
    expect(nav).toHaveBeenCalledWith(['/courses/new']);
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
