import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
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
  // GET-by-id result the inline save reads to preserve the N-N arrays the list projection omits.
  const fullCourse: Course = { ...course(1, 'Azure 基礎'), certificationPkids: [7, 8], jobCategoryPkids: [3] };
  // PUT result (fully-joined row) the save uses to refresh the edited row.
  const savedCourse: Course = { ...fullCourse, title: '新標題' };

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<CourseService>('CourseService', ['list', 'query', 'delete', 'get', 'update']);
    service.list.and.returnValue(of(rows));
    service.query.and.returnValue(of([rows[0]]));
    service.delete.and.returnValue(of(void 0));
    service.get.and.returnValue(of(fullCourse));
    service.update.and.returnValue(of(savedCourse));

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

  describe('inline editing', () => {
    /** The <td> elements of the first data row, in header order. */
    function firstRowCells(): NodeListOf<HTMLTableCellElement> {
      const row = fixture.nativeElement.querySelectorAll('tbody tr')[0] as HTMLElement;
      return row.querySelectorAll('td');
    }

    it('enters edit mode on double-click but not on single-click', () => {
      const displayOrderCell = firstRowCells()[1]; // 0=pkid, 1=顯示順序

      displayOrderCell.dispatchEvent(new MouseEvent('click', { bubbles: true }));
      expect(component['editing']()).toBeNull();

      displayOrderCell.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
      expect(component['editing']()).toEqual({ pkid: 1, field: 'displayOrder' });
    });

    it('treats pkid and the two FK-lookup columns as read-only', () => {
      expect(component['isEditable']('pkid')).toBeFalse();
      expect(component['isEditable']('partnerName')).toBeFalse();
      expect(component['isEditable']('courseGroupDescription')).toBeFalse();
      expect(component['isEditable']('title')).toBeTrue();

      // A double-click on a read-only cell (pkid) must not open an editor.
      firstRowCells()[0].dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
      expect(component['editing']()).toBeNull();

      // Even called directly, startEdit refuses a read-only field.
      component['startEdit'](rows[0], 'partnerName');
      expect(component['editing']()).toBeNull();
    });

    it('persists on blur: GETs the row, overlays the edit, PUTs, and preserves N-N arrays', () => {
      component['startEdit'](rows[0], 'title');
      fixture.detectChanges();

      component['editValue'] = '新標題';
      const input = fixture.nativeElement.querySelector('.cell-input') as HTMLInputElement;
      input.dispatchEvent(new Event('blur'));

      expect(service.get).toHaveBeenCalledWith(1);
      expect(service.update).toHaveBeenCalledTimes(1);
      const request = service.update.calls.mostRecent().args[0];
      expect(request.title).toBe('新標題');
      expect(request.pkid).toBe(1);
      expect(request.certificationPkids).toEqual([7, 8]); // preserved, not wiped
      expect(request.jobCategoryPkids).toEqual([3]);

      // Row refreshed from the PUT response.
      expect(component['courses']().find((c) => c.pkid === 1)?.title).toBe('新標題');
      expect(component['editing']()).toBeNull();
    });

    it('blocks a cleared required field and stays in edit mode', () => {
      component['startEdit'](rows[0], 'title');
      component['editValue'] = '   ';
      component['commitEdit']();

      expect(component['editError']()).toBeTruthy();
      expect(component['editing']()).not.toBeNull();
      expect(service.update).not.toHaveBeenCalled();
    });

    it('blocks a negative numeric field', () => {
      component['startEdit'](rows[0], 'hour');
      component['editValue'] = -5;
      component['commitEdit']();

      expect(component['editError']()).toContain('負');
      expect(service.update).not.toHaveBeenCalled();
    });

    it('blocks an invalid date', () => {
      component['startEdit'](rows[0], 'scheduleOn');
      component['editValue'] = new Date('not-a-date');
      component['commitEdit']();

      expect(component['editError']()).toBeTruthy();
      expect(service.update).not.toHaveBeenCalled();
    });

    it('blocks 上架日期 later than 下架日期', () => {
      // row: scheduleOn 2026-01-01, scheduleOff 2036-01-01 — set scheduleOn beyond scheduleOff.
      component['startEdit'](rows[0], 'scheduleOn');
      component['editValue'] = new Date('2037-01-01T00:00:00');
      component['commitEdit']();

      expect(component['editError']()).toContain('晚於');
      expect(service.update).not.toHaveBeenCalled();
    });

    it('reverts the row and surfaces an error when the save fails', () => {
      service.update.and.returnValue(throwError(() => new Error('boom')));

      component['startEdit'](rows[0], 'title');
      component['editValue'] = '壞掉的標題';
      component['commitEdit']();

      expect(service.update).toHaveBeenCalledTimes(1);
      // Row is left at its previous value (never optimistically mutated) and the editor closes.
      expect(component['courses']().find((c) => c.pkid === 1)?.title).toBe('Azure 基礎');
      expect(component['editing']()).toBeNull();
    });
  });
});
