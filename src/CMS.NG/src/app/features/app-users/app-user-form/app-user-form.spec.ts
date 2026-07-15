import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { AppUserForm } from './app-user-form';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser, AppRoleLookup } from '@core/models/app-user.model';

const roles: AppRoleLookup[] = [
  { roleId: 'Admin', roleName: '系統管理員' },
  { roleId: 'Editor', roleName: '編輯者' },
];

const helen: AppUser = {
  pkid: 1, userId: 'helen', userName: 'Helen', isActive: true,
  passwordUpdatedTime: null, roleCount: 2, roleIds: ['Admin', 'Editor'],
};

function setup(paramId: string | null) {
  const service = jasmine.createSpyObj<AppUserService>('AppUserService', ['get', 'create', 'update']);
  service.get.and.returnValue(of(helen));
  service.create.and.returnValue(of({ ...helen, userId: 'newbie' }));
  service.update.and.returnValue(of(helen));

  const lookups = jasmine.createSpyObj<LookupService>('LookupService', ['appRoles']);
  lookups.appRoles.and.returnValue(of(roles));

  TestBed.configureTestingModule({
    imports: [AppUserForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      MessageService,
      { provide: AppUserService, useValue: service },
      { provide: LookupService, useValue: lookups },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(paramId ? { id: paramId } : {}) } },
      },
    ],
  });

  const fixture: ComponentFixture<AppUserForm> = TestBed.createComponent(AppUserForm);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, service, lookups };
}

describe('AppUserForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('builds an empty form (active by default) in add mode', () => {
    const { component, service } = setup(null);
    expect(component['isEdit']()).toBeFalse();
    expect(service.get).not.toHaveBeenCalled();
    expect(component['form'].getRawValue().isActive).toBeTrue();
    expect(component['roleOptions']().length).toBe(2);
    expect(component['roleOptions']()[0].label).toBe('系統管理員 (Admin)');
  });

  it('loads the user and disables UserId in edit mode', () => {
    const { component, service } = setup('helen');
    expect(component['isEdit']()).toBeTrue();
    expect(service.get).toHaveBeenCalledWith('helen');
    expect(component['form'].getRawValue().userName).toBe('Helen');
    expect(component['form'].controls.userId.disabled).toBeTrue();
    expect(component['form'].getRawValue().roleIds).toEqual(['Admin', 'Editor']);
  });

  it('does not submit when required fields are missing', () => {
    const { component, service } = setup(null);
    component['save']();
    expect(service.create).not.toHaveBeenCalled();
    expect(component['form'].controls.userName.touched).toBeTrue();
  });

  it('maps the form to a request and creates in add mode', () => {
    const { component, service } = setup(null);
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['form'].patchValue({
      userId: 'newbie', userName: 'Newbie', isActive: true, roleIds: ['Admin'],
    });
    component['save']();
    expect(service.create).toHaveBeenCalledWith({
      userId: 'newbie', userName: 'Newbie', isActive: true, roleIds: ['Admin'],
    });
    expect(nav).toHaveBeenCalledWith(['/app-users', 'newbie']);
  });

  it('calls update in edit mode', () => {
    const { component, service } = setup('helen');
    component['save']();
    expect(service.update).toHaveBeenCalled();
    const arg = service.update.calls.mostRecent().args[0];
    expect(arg.userId).toBe('helen');
  });
});
