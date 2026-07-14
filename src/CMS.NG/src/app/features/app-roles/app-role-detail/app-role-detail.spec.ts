import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { AppRoleDetail } from './app-role-detail';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole, AppUserLookup } from '@core/models/app-role.model';

const users: AppUserLookup[] = [
  { userId: 'helen', userName: 'helen' },
  { userId: 'miles@uuu.com.tw', userName: 'Miles Sun' },
];

const role: AppRole = {
  pkid: 1, roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1,
  description: '系統管理員', userCount: 2, userIds: ['helen', 'miles@uuu.com.tw'],
};

describe('AppRoleDetail', () => {
  let fixture: ComponentFixture<AppRoleDetail>;
  let component: AppRoleDetail;
  let service: jasmine.SpyObj<AppRoleService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['get']);
    service.get.and.returnValue(of(role));
    const lookups = jasmine.createSpyObj<LookupService>('LookupService', ['appUsers']);
    lookups.appUsers.and.returnValue(of(users));

    await TestBed.configureTestingModule({
      imports: [AppRoleDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AppRoleService, useValue: service },
        { provide: LookupService, useValue: lookups },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'Admin' }) } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the role by id from the route', () => {
    expect(service.get).toHaveBeenCalledWith('Admin');
    expect(component['role']()?.roleName).toBe('Administrator');
  });

  it('maps user ids to "UserName (UserId)" labels', () => {
    expect(component['userLabels']()).toEqual(['helen (helen)', 'Miles Sun (miles@uuu.com.tw)']);
  });

  it('navigates to edit', () => {
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['edit']();
    expect(nav).toHaveBeenCalledWith(['/app-roles', 'Admin', 'edit']);
  });
});
