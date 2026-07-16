import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { AppUserDetail } from './app-user-detail';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser, AppRoleLookup } from '@core/models/app-user.model';

const roles: AppRoleLookup[] = [
  { roleId: 'Admin', roleName: '系統管理員' },
  { roleId: 'Editor', roleName: '編輯者' },
];

const user: AppUser = {
  pkid: 1, userId: 'helen', userName: 'Helen', isActive: true,
  passwordUpdatedTime: null, roleCount: 2, roleIds: ['Admin', 'Editor'],
};

describe('AppUserDetail', () => {
  let fixture: ComponentFixture<AppUserDetail>;
  let component: AppUserDetail;
  let service: jasmine.SpyObj<AppUserService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['get', 'resetPassword']);
    service.get.and.returnValue(of(user));
    service.resetPassword.and.returnValue(of(void 0));
    const lookups = jasmine.createSpyObj<LookupService>('LookupService', ['appRoles']);
    lookups.appRoles.and.returnValue(of(roles));

    await TestBed.configureTestingModule({
      imports: [AppUserDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: RowAuditService, useValue: { history: () => of([]) } },
        ConfirmationService,
        MessageService,
        { provide: AppUserService, useValue: service },
        { provide: LookupService, useValue: lookups },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'helen' }) } } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the user by id from the route', () => {
    expect(service.get).toHaveBeenCalledWith('helen');
    expect(component['user']()?.userName).toBe('Helen');
  });

  it('maps role ids to "RoleName (RoleId)" labels', () => {
    expect(component['roleLabels']()).toEqual(['系統管理員 (Admin)', '編輯者 (Editor)']);
  });

  it('resets the password through the service on confirm', () => {
    const confirm = TestBed.inject(ConfirmationService);
    spyOn(confirm, 'confirm').and.callFake((opts) => {
      opts.accept?.();
      return confirm;
    });
    component['confirmResetPassword']();
    expect(service.resetPassword).toHaveBeenCalledWith('helen');
  });

  it('navigates to edit', () => {
    const nav = spyOn(TestBed.inject(Router), 'navigate');
    component['edit']();
    expect(nav).toHaveBeenCalledWith(['/app-users', 'helen', 'edit']);
  });
});
