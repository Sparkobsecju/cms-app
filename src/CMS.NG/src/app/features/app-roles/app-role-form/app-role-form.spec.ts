import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { AppRoleForm } from './app-role-form';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole, AppUserLookup } from '@core/models/app-role.model';

const users: AppUserLookup[] = [
  { userId: 'helen', userName: 'helen' },
  { userId: 'miles@uuu.com.tw', userName: 'Miles Sun' },
];

const adminRole: AppRole = {
  pkid: 1, roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1,
  description: '系統管理員', userCount: 2, userIds: ['helen', 'miles@uuu.com.tw'],
};

function setup(paramId: string | null) {
  const service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['get', 'create', 'update']);
  service.get.and.returnValue(of(adminRole));
  service.create.and.returnValue(of({ ...adminRole, roleId: 'Editor' }));
  service.update.and.returnValue(of(adminRole));

  const lookups = jasmine.createSpyObj<LookupService>('LookupService', ['appUsers']);
  lookups.appUsers.and.returnValue(of(users));

  TestBed.configureTestingModule({
    imports: [AppRoleForm],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      MessageService,
      { provide: AppRoleService, useValue: service },
      { provide: LookupService, useValue: lookups },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(paramId ? { id: paramId } : {}) } },
      },
    ],
  });

  const fixture: ComponentFixture<AppRoleForm> = TestBed.createComponent(AppRoleForm);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, service, lookups };
}

describe('AppRoleForm', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('builds an empty form with default permission level in add mode', () => {
    const { component, service } = setup(null);
    expect(component['isEdit']()).toBeFalse();
    expect(service.get).not.toHaveBeenCalled();
    expect(component['form'].getRawValue().permissionLevel).toBe(100);
    expect(component['userOptions']().length).toBe(2);
    expect(component['userOptions']()[0].label).toBe('helen (helen)');
  });

  it('loads the role and disables RoleId in edit mode', () => {
    const { component, service } = setup('Admin');
    expect(component['isEdit']()).toBeTrue();
    expect(service.get).toHaveBeenCalledWith('Admin');
    expect(component['form'].getRawValue().roleName).toBe('Administrator');
    expect(component['form'].controls.roleId.disabled).toBeTrue();
    expect(component['form'].getRawValue().userIds).toEqual(['helen', 'miles@uuu.com.tw']);
  });

  it('does not submit when required fields are missing', () => {
    const { component, service } = setup(null);
    component['save']();
    expect(service.create).not.toHaveBeenCalled();
    expect(component['form'].controls.roleName.touched).toBeTrue();
  });

  it('maps the form to a request and creates in add mode', () => {
    const { component, service } = setup(null);
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['form'].patchValue({
      roleId: 'Editor', roleName: 'Editor', permissionLevel: 50,
      description: '  ', userIds: ['helen'],
    });
    component['save']();
    expect(service.create).toHaveBeenCalledWith({
      roleId: 'Editor', roleName: 'Editor', permissionLevel: 50, description: null, userIds: ['helen'],
    });
    expect(nav).toHaveBeenCalledWith(['/app-roles', 'Editor']);
  });

  it('calls update in edit mode', () => {
    const { component, service } = setup('Admin');
    component['save']();
    expect(service.update).toHaveBeenCalled();
    const arg = service.update.calls.mostRecent().args[0];
    expect(arg.roleId).toBe('Admin');
  });
});
