import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { Login } from './login';
import { AuthService } from '@core/services/auth.service';

interface LoginInternals {
  form: { setValue: (v: { userId: string; password: string }) => void };
  submit: () => void;
  errorMessage: () => string | null;
}

describe('Login', () => {
  let auth: jasmine.SpyObj<AuthService>;

  async function createComponent() {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        { provide: AuthService, useValue: auth },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Login);
    return fixture;
  }

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['login']);
  });

  it('does not call login when the form is empty/invalid', async () => {
    const fixture = await createComponent();
    (fixture.componentInstance as unknown as LoginInternals).submit();
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('posts the credentials and navigates home on success', async () => {
    auth.login.and.returnValue(of({ userId: 'helen', userName: 'Helen', accessToken: 't' }));
    const fixture = await createComponent();
    const router = TestBed.inject(Router);
    const nav = spyOn(router, 'navigate');

    const comp = fixture.componentInstance as unknown as LoginInternals;
    comp.form.setValue({ userId: 'helen', password: 'secret' });
    comp.submit();

    expect(auth.login).toHaveBeenCalledWith('helen', 'secret');
    expect(nav).toHaveBeenCalledWith(['/']);
  });

  it('shows a generic error message on a failed login', async () => {
    auth.login.and.returnValue(throwError(() => ({ status: 401 })));
    const fixture = await createComponent();

    const comp = fixture.componentInstance as unknown as LoginInternals;
    comp.form.setValue({ userId: 'helen', password: 'wrong' });
    comp.submit();

    expect(comp.errorMessage()).toContain('Invalid credentials');
  });
});
