import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { CourseGroupForm } from './course-group-form';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';

const group: CourseGroup = { pkid: 1, description: '資訊技術' };

function setup(paramId: string | null) {
  const service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['get', 'create', 'update']);
  service.get.and.returnValue(of(group));
  service.create.and.returnValue(of({ pkid: 5, description: '數位轉型' }));
  service.update.and.returnValue(of(group));

  TestBed.configureTestingModule({
    imports: [CourseGroupForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      { provide: RowAuditService, useValue: { history: () => of([]) } },
      MessageService,
      { provide: CourseGroupService, useValue: service },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(paramId ? { id: paramId } : {}) } },
      },
    ],
  });

  const fixture: ComponentFixture<CourseGroupForm> = TestBed.createComponent(CourseGroupForm);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, service };
}

describe('CourseGroupForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('builds an empty form in add mode', () => {
    const { component, service } = setup(null);
    expect(component['isEdit']()).toBeFalse();
    expect(service.get).not.toHaveBeenCalled();
    expect(component['form'].getRawValue().description).toBe('');
  });

  it('loads the group and captures pkid in edit mode', () => {
    const { component, service } = setup('1');
    expect(component['isEdit']()).toBeTrue();
    expect(service.get).toHaveBeenCalledWith(1);
    expect(component['form'].getRawValue().description).toBe('資訊技術');
    expect(component['pkid']()).toBe(1);
  });

  it('does not submit when required fields are missing', () => {
    const { component, service } = setup(null);
    component['save']();
    expect(service.create).not.toHaveBeenCalled();
    expect(component['form'].controls.description.touched).toBeTrue();
  });

  it('maps the form to a request and creates in add mode', () => {
    const { component, service } = setup(null);
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['form'].patchValue({ description: '  數位轉型  ' });
    component['save']();
    expect(service.create).toHaveBeenCalledWith({ pkid: 0, description: '數位轉型' });
    expect(nav).toHaveBeenCalledWith(['/course-groups', 5]);
  });

  it('calls update in edit mode with the captured pkid', () => {
    const { component, service } = setup('1');
    component['save']();
    expect(service.update).toHaveBeenCalled();
    const arg = service.update.calls.mostRecent().args[0];
    expect(arg.pkid).toBe(1);
  });
});
