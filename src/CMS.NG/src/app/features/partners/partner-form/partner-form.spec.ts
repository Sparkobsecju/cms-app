import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { PartnerForm } from './partner-form';
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

function setup(paramId: string | null) {
  const service = jasmine.createSpyObj<PartnerService>('PartnerService', ['get', 'create', 'update']);
  service.get.and.returnValue(of(partner));
  service.create.and.returnValue(of({ ...partner, pkid: 5, name: 'Amazon' }));
  service.update.and.returnValue(of(partner));

  TestBed.configureTestingModule({
    imports: [PartnerForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      { provide: RowAuditService, useValue: { history: () => of([]) } },
      MessageService,
      { provide: PartnerService, useValue: service },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(paramId ? { id: paramId } : {}) } },
      },
    ],
  });

  const fixture: ComponentFixture<PartnerForm> = TestBed.createComponent(PartnerForm);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, service };
}

describe('PartnerForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('builds an empty form in add mode', () => {
    const { component, service } = setup(null);
    expect(component['isEdit']()).toBeFalse();
    expect(service.get).not.toHaveBeenCalled();
    expect(component['form'].getRawValue().name).toBe('');
  });

  it('loads the partner and captures pkid in edit mode', () => {
    const { component, service } = setup('1');
    expect(component['isEdit']()).toBeTrue();
    expect(service.get).toHaveBeenCalledWith(1);
    expect(component['form'].getRawValue().name).toBe('Microsoft');
    expect(component['pkid']()).toBe(1);
  });

  it('does not submit when required fields are missing', () => {
    const { component, service } = setup(null);
    component['save']();
    expect(service.create).not.toHaveBeenCalled();
    expect(component['form'].controls.name.touched).toBeTrue();
  });

  it('maps the form to a request and creates in add mode', () => {
    const { component, service } = setup(null);
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['form'].patchValue({
      name: '  Amazon  ',
      appKey: 'AWS',
      nameOnPartnerMenu: 'Amazon 選單',
      nameOnCourseDetailPage: 'Amazon',
      displayOrder: 2,
      imageFilename: '',
    });
    component['save']();
    expect(service.create).toHaveBeenCalledWith({
      pkid: 0,
      name: 'Amazon',
      appKey: 'AWS',
      nameOnPartnerMenu: 'Amazon 選單',
      nameOnCourseDetailPage: 'Amazon',
      displayOrder: 2,
      imageFilename: null,
    });
    expect(nav).toHaveBeenCalledWith(['/partners', 5]);
  });

  it('calls update in edit mode with the captured pkid', () => {
    const { component, service } = setup('1');
    component['save']();
    expect(service.update).toHaveBeenCalled();
    const arg = service.update.calls.mostRecent().args[0];
    expect(arg.pkid).toBe(1);
  });
});
