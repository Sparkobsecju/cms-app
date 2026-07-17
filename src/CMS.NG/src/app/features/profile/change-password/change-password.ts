import { Component, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { AuthService } from '@core/services/auth.service';

/** Bilingual complexity message — mirrors the server's `PasswordPolicy.Requirement`. */
export const PASSWORD_COMPLEXITY_MESSAGE =
  '密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號 ' +
  '(Password must be at least 8 characters and contain at least 3 of the 4 classes: ' +
  'uppercase / lowercase / digit / symbol.)';

/**
 * Client-side mirror of the server's password policy: at least 8 characters and at least 3 of the 4
 * character classes (uppercase / lowercase / digit / symbol). A "symbol" is anything that is not a
 * letter or a digit. Empty values are left to the `required` validator. Exported for unit testing.
 */
export function passwordComplexityValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = String(control.value ?? '');
    if (value.length === 0) {
      return null;
    }
    const hasUpper = /[A-Z]/.test(value);
    const hasLower = /[a-z]/.test(value);
    const hasDigit = /[0-9]/.test(value);
    const hasSymbol = /[^A-Za-z0-9]/.test(value);
    const classes = [hasUpper, hasLower, hasDigit, hasSymbol].filter(Boolean).length;
    return value.length >= 8 && classes >= 3 ? null : { complexity: true };
  };
}

/**
 * Validates that the `newPassword` and `confirmPassword` controls match. Applied at the group level so it
 * re-runs whenever either field changes; sets a `mismatch` error on the group. Exported for unit testing.
 */
export const passwordsMatchValidator: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const next = group.get('newPassword')?.value;
  const confirm = group.get('confirmPassword')?.value;
  // Don't flag a mismatch until the user has typed a confirmation.
  return !confirm || next === confirm ? null : { mismatch: true };
};

/**
 * Change Password card on the My Profile page (變更密碼). Collects the current password, a new password and
 * its confirmation, validates the new password against the same complexity policy the backend enforces,
 * and posts plaintext (never a hash) to {@link AuthService.changePassword}. The backend re-verifies
 * everything and takes the target UserId from the JWT; a server-side rejection (e.g. wrong current
 * password) is surfaced inline from the response message.
 */
@Component({
  selector: 'app-change-password',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './change-password.html',
  styleUrl: './change-password.scss',
})
export class ChangePassword {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly messageService = inject(MessageService);

  protected readonly submitting = signal(false);
  /** Error returned by the server (e.g. wrong current password), shown inline. Cleared on edit/submit. */
  protected readonly serverError = signal('');

  /** The signed-in UserId, bound to a hidden username field so password managers can associate the
   * credential (and to satisfy the accessibility guidance that password forms carry a username field). */
  protected readonly userId = computed(() => this.auth.profile()?.userId ?? '');

  protected readonly complexityMessage = PASSWORD_COMPLEXITY_MESSAGE;

  protected readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, passwordComplexityValidator()]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatchValidator },
  );

  protected submit(): void {
    this.serverError.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword, confirmPassword } = this.form.getRawValue();
    this.submitting.set(true);
    this.auth.changePassword(currentPassword, newPassword, confirmPassword).subscribe({
      next: () => {
        this.submitting.set(false);
        this.form.reset();
        this.messageService.add({
          severity: 'success',
          summary: '已變更 Changed',
          detail: '密碼已更新。 Password updated.',
        });
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        // 400s carry a safe, user-facing message from the server; show it inline.
        this.serverError.set(serverMessage(error));
      },
    });
  }

  /** Whether the given control should show its validation error (invalid and dirtied/touched). */
  protected isInvalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.dirty || c.touched);
  }

  /** Whether to show the new/confirm mismatch message (group-level error, once confirm is touched). */
  protected showMismatch(): boolean {
    const confirm = this.form.get('confirmPassword');
    return this.form.hasError('mismatch') && !!confirm && (confirm.dirty || confirm.touched);
  }
}

/** Reads the server's `message` from a 400 response, falling back to a bilingual generic message. */
function serverMessage(error: HttpErrorResponse): string {
  const body = error.error;
  if (body && typeof body === 'object' && typeof (body as { message?: unknown }).message === 'string') {
    return (body as { message: string }).message;
  }
  return '無法變更密碼，請稍後再試。 Could not change the password. Please try again.';
}
