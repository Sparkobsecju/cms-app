import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { PublishStatusForm } from './publish-status-form';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';

const draft: PublishStatus = {
  pkid: 1, description: 'Draft', isDraft: true, isPublished: false, isDiscontinued: false,
};

function setup(paramId: string | null) {
  const service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['get', 'create', 'update']);
  service.get.and.returnValue(of(draft));
  service.create.and.returnValue(of({ ...draft, pkid: 3, description: 'Archived' }));
  service.update.and.returnValue(of(draft));

  TestBed.configureTestingModule({
    imports: [PublishStatusForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      MessageService,
      { provide: PublishStatusService, useValue: service },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(paramId ? { id: paramId } : {}) } },
      },
    ],
  });

  const fixture: ComponentFixture<PublishStatusForm> = TestBed.createComponent(PublishStatusForm);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, service };
}

describe('PublishStatusForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('builds an empty form in add mode', () => {
    const { component, service } = setup(null);
    expect(component['isEdit']()).toBeFalse();
    expect(service.get).not.toHaveBeenCalled();
    expect(component['form'].getRawValue().pkid).toBeNull();
  });

  it('loads the status and disables pkid in edit mode', () => {
    const { component, service } = setup('1');
    expect(component['isEdit']()).toBeTrue();
    expect(service.get).toHaveBeenCalledWith(1);
    expect(component['form'].getRawValue().description).toBe('Draft');
    expect(component['form'].controls.pkid.disabled).toBeTrue();
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
    component['form'].patchValue({
      pkid: 3, description: '  Archived  ', isDraft: false, isPublished: false, isDiscontinued: true,
    });
    component['save']();
    expect(service.create).toHaveBeenCalledWith({
      pkid: 3, description: 'Archived', isDraft: false, isPublished: false, isDiscontinued: true,
    });
    expect(nav).toHaveBeenCalledWith(['/publish-statuses', 3]);
  });

  it('calls update in edit mode', () => {
    const { component, service } = setup('1');
    component['save']();
    expect(service.update).toHaveBeenCalled();
    const arg = service.update.calls.mostRecent().args[0];
    expect(arg.pkid).toBe(1);
  });
});
