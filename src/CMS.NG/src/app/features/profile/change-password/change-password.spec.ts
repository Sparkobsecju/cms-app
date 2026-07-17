import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { FormGroup } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { MessageService } from 'primeng/api';
import {
  ChangePassword,
  PASSWORD_COMPLEXITY_MESSAGE,
  passwordComplexityValidator,
  passwordsMatchValidator,
} from './change-password';
import { AuthService } from '@core/services/auth.service';

interface ChangePasswordInternals {
  form: FormGroup;
  submit: () => void;
  serverError: () => string;
}

describe('ChangePassword', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let messageService: jasmine.SpyObj<MessageService>;

  async function createComponent() {
    await TestBed.configureTestingModule({
      imports: [ChangePassword],
      providers: [
        provideNoopAnimations(),
        { provide: AuthService, useValue: auth },
        { provide: MessageService, useValue: messageService },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(ChangePassword);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['changePassword'], {
      // The hidden username field reads the signed-in UserId from the profile signal.
      profile: signal({ userId: 'helen', userName: 'Helen', accessToken: 't' }),
    });
    messageService = jasmine.createSpyObj<MessageService>('MessageService', ['add']);
  });

  // ----- Pure validators (the policy mirrored from the backend) -----

  describe('passwordComplexityValidator', () => {
    const validate = (value: string) =>
      passwordComplexityValidator()({ value } as never);

    it('accepts an empty value (left to the required validator)', () => {
      expect(validate('')).toBeNull();
    });

    it('rejects passwords shorter than 8 characters even with 4 classes', () => {
      expect(validate('Ab1!')).toEqual({ complexity: true });
    });

    it('rejects an 8-char password with fewer than 3 classes', () => {
      expect(validate('password')).toEqual({ complexity: true }); // 1 class
      expect(validate('Password')).toEqual({ complexity: true }); // 2 classes
      expect(validate('PASSWORD123')).toEqual({ complexity: true }); // 2 classes
    });

    it('accepts >= 8 chars with at least 3 of the 4 classes', () => {
      expect(validate('Abc12345')).toBeNull(); // upper, lower, digit
      expect(validate('abc123!@')).toBeNull(); // lower, digit, symbol
      expect(validate('NewPass9#')).toBeNull(); // all four
    });
  });

  describe('passwordsMatchValidator', () => {
    const group = (next: string, confirm: string) =>
      ({ get: (name: string) => ({ value: name === 'newPassword' ? next : confirm }) }) as never;

    it('passes when the confirmation is empty (not yet typed)', () => {
      expect(passwordsMatchValidator(group('NewPass9#', ''))).toBeNull();
    });

    it('passes when new and confirm match', () => {
      expect(passwordsMatchValidator(group('NewPass9#', 'NewPass9#'))).toBeNull();
    });

    it('flags a mismatch when new and confirm differ', () => {
      expect(passwordsMatchValidator(group('NewPass9#', 'NewPass9$'))).toEqual({ mismatch: true });
    });
  });

  // ----- Form-level client validation -----

  it('is invalid while any field is empty', async () => {
    const comp = (await createComponent()).componentInstance as unknown as ChangePasswordInternals;
    expect(comp.form.invalid).toBeTrue();
  });

  it('is invalid when the new password fails complexity', async () => {
    const comp = (await createComponent()).componentInstance as unknown as ChangePasswordInternals;
    comp.form.setValue({ currentPassword: 'anything', newPassword: 'weak', confirmPassword: 'weak' });
    expect(comp.form.get('newPassword')?.hasError('complexity')).toBeTrue();
    expect(comp.form.invalid).toBeTrue();
  });

  it('is invalid when new and confirm do not match', async () => {
    const comp = (await createComponent()).componentInstance as unknown as ChangePasswordInternals;
    comp.form.setValue({ currentPassword: 'anything', newPassword: 'NewPass9#', confirmPassword: 'NewPass9$' });
    expect(comp.form.hasError('mismatch')).toBeTrue();
    expect(comp.form.invalid).toBeTrue();
  });

  it('is valid when all fields pass and new === confirm', async () => {
    const comp = (await createComponent()).componentInstance as unknown as ChangePasswordInternals;
    comp.form.setValue({ currentPassword: 'OldPass1!', newPassword: 'NewPass9#', confirmPassword: 'NewPass9#' });
    expect(comp.form.valid).toBeTrue();
  });

  it('does not call the API when the form is invalid', async () => {
    const comp = (await createComponent()).componentInstance as unknown as ChangePasswordInternals;
    comp.form.setValue({ currentPassword: '', newPassword: 'weak', confirmPassword: 'nope' });

    comp.submit();

    expect(auth.changePassword).not.toHaveBeenCalled();
  });

  it('posts the three plaintext fields and shows a success toast on a valid change', async () => {
    auth.changePassword.and.returnValue(of(void 0));
    const comp = (await createComponent()).componentInstance as unknown as ChangePasswordInternals;
    comp.form.setValue({ currentPassword: 'OldPass1!', newPassword: 'NewPass9#', confirmPassword: 'NewPass9#' });

    comp.submit();

    expect(auth.changePassword).toHaveBeenCalledWith('OldPass1!', 'NewPass9#', 'NewPass9#');
    expect(messageService.add).toHaveBeenCalled();
  });

  it('surfaces the server message inline when the backend rejects the change', async () => {
    const message = '目前密碼不正確。 Current password is incorrect.';
    auth.changePassword.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { message } })),
    );
    const comp = (await createComponent()).componentInstance as unknown as ChangePasswordInternals;
    comp.form.setValue({ currentPassword: 'wrong', newPassword: 'NewPass9#', confirmPassword: 'NewPass9#' });

    comp.submit();

    expect(comp.serverError()).toBe(message);
    expect(messageService.add).not.toHaveBeenCalled();
  });

  it('exposes the bilingual complexity message shown in the UI', () => {
    expect(PASSWORD_COMPLEXITY_MESSAGE).toContain('至少');
    expect(PASSWORD_COMPLEXITY_MESSAGE).toContain('uppercase / lowercase / digit / symbol');
  });

  // Regression: QA-ISSUE-003 — the change-password form had password fields but no username field,
  // which browsers flag ("password forms should have a username field for accessibility") and which
  // stops password managers associating the new credential. A hidden username field bound to the
  // signed-in UserId must be present. Found by /qa on 2026-07-17.
  // Report: qa/qa-report-localhost-2026-07-17.md
  it('includes a hidden username field carrying the signed-in UserId for accessibility', async () => {
    const el = (await createComponent()).nativeElement as HTMLElement;
    const username = el.querySelector<HTMLInputElement>('input[autocomplete="username"]');
    expect(username).withContext('hidden username input is present').not.toBeNull();
    expect(username!.value).toBe('helen');
    // Must be rendered (off-screen), not display:none, so managers/a11y still see it.
    expect(getComputedStyle(username!).display).not.toBe('none');
  });
});
