import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { AppRoleList } from './app-role-list';
import { AppRoleService } from '@core/services/app-role.service';
import { AppRole } from '@core/models/app-role.model';

describe('AppRoleList', () => {
  let fixture: ComponentFixture<AppRoleList>;
  let component: AppRoleList;
  let service: jasmine.SpyObj<AppRoleService>;
  let router: Router;

  const rows: AppRole[] = [
    { pkid: 1, roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1, description: '系統管理員', userCount: 3, userIds: [] },
    { pkid: 2, roleId: 'User', roleName: 'User', permissionLevel: 100, description: '一般使用者', userCount: 9, userIds: [] },
  ];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['list', 'query', 'delete']);
    service.list.and.returnValue(of(rows));
    service.query.and.returnValue(of([rows[0]]));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppRoleList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        ConfirmationService,
        MessageService,
        { provide: AppRoleService, useValue: service },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('loads all roles on init', () => {
    expect(service.list).toHaveBeenCalled();
    expect(component['roles']().length).toBe(2);
  });

  it('renders a row per role', () => {
    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(bodyRows.length).toBe(2);
  });

  it('applies a keyword filter via query()', () => {
    component['filter'] = { keyword: 'adm', permissionLevel: null };
    component['applyFilter']();
    expect(service.query).toHaveBeenCalledWith({ keyword: 'adm', permissionLevel: null });
    expect(component['roles']().length).toBe(1);
  });

  it('navigates to the add page', () => {
    const nav = spyOn(router, 'navigate');
    component['add']();
    expect(nav).toHaveBeenCalledWith(['/app-roles/new']);
  });

  it('deletes through the service on confirm', () => {
    const confirm = TestBed.inject(ConfirmationService);
    spyOn(confirm, 'confirm').and.callFake((opts) => {
      opts.accept?.();
      return confirm;
    });
    component['confirmDelete'](rows[1]);
    expect(service.delete).toHaveBeenCalledWith('User');
  });
});
