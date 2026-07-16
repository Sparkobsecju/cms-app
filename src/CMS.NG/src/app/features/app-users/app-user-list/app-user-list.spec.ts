import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { AppUserList } from './app-user-list';
import { AppUserService } from '@core/services/app-user.service';
import { AppUser } from '@core/models/app-user.model';

describe('AppUserList', () => {
  let fixture: ComponentFixture<AppUserList>;
  let component: AppUserList;
  let service: jasmine.SpyObj<AppUserService>;
  let router: Router;

  const rows: AppUser[] = [
    { pkid: 1, userId: 'helen', userName: 'Helen', isActive: true, passwordUpdatedTime: null, roleCount: 2, roleIds: [] },
    { pkid: 2, userId: 'miles', userName: 'Miles', isActive: false, passwordUpdatedTime: null, roleCount: 0, roleIds: [] },
  ];

  beforeEach(async () => {
    sessionStorage.clear();
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['list', 'query', 'delete']);
    service.list.and.returnValue(of(rows));
    service.query.and.returnValue(of([rows[0]]));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppUserList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        ConfirmationService,
        MessageService,
        { provide: AppUserService, useValue: service },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserList);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('loads all users on init', () => {
    expect(service.list).toHaveBeenCalled();
    expect(component['users']().length).toBe(2);
  });

  it('renders a row per user', () => {
    const bodyRows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(bodyRows.length).toBe(2);
  });

  it('applies an active filter via query()', () => {
    component['filter'] = { keyword: null, isActive: true };
    component['applyFilter']();
    expect(service.query).toHaveBeenCalledWith({ keyword: null, isActive: true });
    expect(component['users']().length).toBe(1);
  });

  it('navigates to the add page', () => {
    const nav = spyOn(router, 'navigate');
    component['add']();
    expect(nav).toHaveBeenCalledWith(['/app-users/new']);
  });

  it('deletes through the service on confirm', () => {
    const confirm = TestBed.inject(ConfirmationService);
    spyOn(confirm, 'confirm').and.callFake((opts) => {
      opts.accept?.();
      return confirm;
    });
    component['confirmDelete'](rows[1]);
    expect(service.delete).toHaveBeenCalledWith('miles');
  });
});
