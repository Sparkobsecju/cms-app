import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { CourseForm } from './course-form';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course } from '@core/models/course.model';

const course: Course = {
  pkid: 1, title: 'Azure 基礎', officialTitle: null, courseId: 'C001', prodCourseId: 'P001',
  friendlyUrl: 'azure-basics', displayOrder: 1, partnerPkid: 1, courseGroupPkid: null,
  publishStatusPkid: 1, scheduleOn: '2026-01-01', scheduleOff: '2036-01-01', hour: 8,
  listPrice: 12000, learningCredit: 3.5, material: null, objective: null, target: null,
  prerequisites: null, outline: null, towardCertOrExam: null, note: null, otherInfo: null,
  canRepeat: true, partnerName: 'Microsoft', courseGroupDescription: null,
  publishStatusDescription: '已發布', certificationPkids: [], jobCategoryPkids: [],
};

function setup(paramId: string | null) {
  const service = jasmine.createSpyObj<CourseService>('CourseService', ['get', 'create', 'update']);
  service.get.and.returnValue(of(course));
  service.create.and.returnValue(of({ ...course, pkid: 5 }));
  service.update.and.returnValue(of(course));

  const lookups = jasmine.createSpyObj<LookupService>('LookupService',
    ['partners', 'courseGroups', 'publishStatuses', 'certifications', 'jobCategories']);
  lookups.partners.and.returnValue(of([{ pkid: 1, name: 'Microsoft' }]));
  lookups.courseGroups.and.returnValue(of([]));
  lookups.publishStatuses.and.returnValue(of([{ pkid: 1, description: '已發布' }]));
  lookups.certifications.and.returnValue(of([]));
  lookups.jobCategories.and.returnValue(of([]));

  TestBed.configureTestingModule({
    imports: [CourseForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      MessageService,
      { provide: CourseService, useValue: service },
      { provide: LookupService, useValue: lookups },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(paramId ? { id: paramId } : {}) } },
      },
    ],
  });

  const fixture: ComponentFixture<CourseForm> = TestBed.createComponent(CourseForm);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, service };
}

/** Fills the required controls so the form validates. */
function fillRequired(component: CourseForm): void {
  component['form'].patchValue({
    title: 'Azure 基礎',
    courseId: 'C001',
    prodCourseId: 'P001',
    friendlyUrl: 'azure-basics',
    displayOrder: 1,
    partnerPkid: 1,
    publishStatusPkid: 1,
    scheduleOn: new Date(2026, 0, 1),
    scheduleOff: new Date(2036, 0, 1),
    hour: 8,
    listPrice: 12000,
    learningCredit: 3.5,
  });
}

/** Asserts the action toolbar is pinned (sticky) and still exposes Save/Cancel. */
function assertStickyToolbar(fixture: ComponentFixture<CourseForm>): void {
  const header = fixture.nativeElement.querySelector('.page-header.sticky-toolbar') as HTMLElement;
  expect(header).withContext('action toolbar').toBeTruthy();
  expect(getComputedStyle(header).position).withContext('pinned toolbar').toBe('sticky');

  const labels = Array.from(header.querySelectorAll('.page-actions button'))
    .map((b) => b.textContent?.trim() ?? '');
  expect(labels.some((l) => l.includes('儲存'))).withContext('Save button present').toBeTrue();
  expect(labels.some((l) => l.includes('取消'))).withContext('Cancel button present').toBeTrue();
}

describe('CourseForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('pins the action toolbar with Save/Cancel in add mode', () => {
    const { fixture } = setup(null);
    assertStickyToolbar(fixture);
  });

  it('pins the action toolbar with Save/Cancel in edit mode', () => {
    const { fixture } = setup('1');
    assertStickyToolbar(fixture);
  });

  it('builds an empty form in add mode', () => {
    const { component, service } = setup(null);
    expect(component['isEdit']()).toBeFalse();
    expect(service.get).not.toHaveBeenCalled();
    expect(component['form'].getRawValue().title).toBe('');
  });

  it('loads the course and captures pkid in edit mode', () => {
    const { component, service } = setup('1');
    expect(component['isEdit']()).toBeTrue();
    expect(service.get).toHaveBeenCalledWith(1);
    expect(component['form'].getRawValue().title).toBe('Azure 基礎');
    expect(component['pkid']()).toBe(1);
  });

  it('does not submit when required fields are missing', () => {
    const { component, service } = setup(null);
    component['save']();
    expect(service.create).not.toHaveBeenCalled();
    expect(component['form'].controls.title.touched).toBeTrue();
  });

  it('maps the form to a request and creates in add mode', () => {
    const { component, service } = setup(null);
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    fillRequired(component);
    component['save']();
    expect(service.create).toHaveBeenCalled();
    const arg = service.create.calls.mostRecent().args[0];
    expect(arg.pkid).toBe(0);
    expect(arg.scheduleOn).toBe('2026-01-01');
    expect(nav).toHaveBeenCalledWith(['/courses', 5]);
  });

  it('calls update in edit mode with the captured pkid', () => {
    const { component, service } = setup('1');
    component['save']();
    expect(service.update).toHaveBeenCalled();
    const arg = service.update.calls.mostRecent().args[0];
    expect(arg.pkid).toBe(1);
  });
});
