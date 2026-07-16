import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { signal } from '@angular/core';
import { ConfirmationService, MessageService } from 'primeng/api';
import { App } from './app';
import { AuthService } from '@core/services/auth.service';

type AuthStub = Pick<AuthService, 'userName' | 'hasRole' | 'isAuthenticated' | 'clearSession'>;

function authStub(overrides: Partial<AuthStub> = {}): AuthStub {
  return {
    userName: signal('Alice'),
    hasRole: (role: string) => role === 'Admin',
    isAuthenticated: () => true,
    clearSession: () => {},
    ...overrides,
  };
}

async function createApp(auth: AuthStub) {
  await TestBed.configureTestingModule({
    imports: [App],
    providers: [
      provideRouter([]),
      provideNoopAnimations(),
      ConfirmationService,
      MessageService,
      { provide: AuthService, useValue: auth },
    ],
  }).compileComponents();
  return TestBed.createComponent(App);
}

describe('App', () => {
  it('should create the app', async () => {
    const fixture = await createApp(authStub());
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the brand and, for an Admin user, the 系統管理 Admin nav group', async () => {
    const fixture = await createApp(authStub({ hasRole: (r) => r === 'Admin' }));
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.app-name')?.textContent).toContain('UWA');
    expect(el.textContent).toContain('系統管理 Admin');
    expect(el.textContent).toContain('角色 AppRole');
  });

  it('hides the 系統管理 Admin nav group for a non-Admin user', async () => {
    const fixture = await createApp(authStub({ hasRole: () => false }));
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).not.toContain('系統管理 Admin');
    expect(el.textContent).not.toContain('角色 AppRole');
  });

  it('shows the signed-in user name in the shell', async () => {
    const fixture = await createApp(authStub({ userName: signal('Helen Wu') }));
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Helen Wu');
  });

  it('shows a My Profile link pointing at /profile', async () => {
    const fixture = await createApp(authStub());
    fixture.detectChanges();
    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('.footer-link');
    expect(link?.textContent).toContain('個人資料 My Profile');
    expect(link?.getAttribute('href')).toContain('/profile');
  });

  it('logout clears the session and navigates to /login', async () => {
    const clearSession = jasmine.createSpy('clearSession');
    const fixture = await createApp(authStub({ clearSession }));
    const router = TestBed.inject(Router);
    const nav = spyOn(router, 'navigate');

    (fixture.componentInstance as unknown as { logout: () => void }).logout();

    expect(clearSession).toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith(['/login']);
  });

  it('toggles the sidebar collapsed state', async () => {
    const fixture = await createApp(authStub());
    const component = fixture.componentInstance as unknown as {
      collapsed: () => boolean;
      toggleCollapsed: () => void;
    };
    expect(component.collapsed()).toBeFalse();
    component.toggleCollapsed();
    expect(component.collapsed()).toBeTrue();
  });
});
