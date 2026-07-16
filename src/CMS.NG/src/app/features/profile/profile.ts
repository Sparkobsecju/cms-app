import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { AuthService } from '@core/services/auth.service';
import { ChangePassword } from './change-password/change-password';

/**
 * My Profile page (個人資料). Shows the signed-in user's UserId and roles read-only, and lets them edit
 * <b>only</b> their own UserName. Saving calls {@link AuthService.updateUserName}, which posts the new
 * name (the backend takes the UserId from the JWT) and, on success, refreshes the name shown in the app
 * shell and in session storage.
 */
@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, ChangePassword],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly messageService = inject(MessageService);

  /** Read-only identity — displayed, never editable through this page. */
  protected readonly userId = computed(() => this.auth.profile()?.userId ?? '');
  protected readonly roles = this.auth.roles;

  protected readonly submitting = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    userName: [this.auth.userName(), [Validators.required]],
  });

  protected save(): void {
    const userName = this.form.controls.userName.value.trim();
    this.form.controls.userName.setValue(userName);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.auth.updateUserName(userName).subscribe({
      next: () => {
        this.submitting.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '已儲存 Saved',
          detail: '個人資料已更新。 Profile updated.',
        });
      },
      error: () => {
        // The global errorInterceptor already surfaces a toast; just release the button.
        this.submitting.set(false);
      },
    });
  }

  protected isInvalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.dirty || c.touched);
  }
}
