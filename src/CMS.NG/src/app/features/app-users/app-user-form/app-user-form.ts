import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { MultiSelectModule } from 'primeng/multiselect';
import { MessageService } from 'primeng/api';
import { forkJoin, of } from 'rxjs';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUserRequest, AppRoleLookup } from '@core/models/app-user.model';

interface RoleOption { roleId: string; label: string; }

/** Add / edit form for a user (新增／編輯使用者). No password field — password is server-managed. */
@Component({
  selector: 'app-app-user-form',
  imports: [
    CommonModule, ReactiveFormsModule, ButtonModule,
    InputTextModule, CheckboxModule, MultiSelectModule,
  ],
  templateUrl: './app-user-form.html',
  styleUrl: './app-user-form.scss',
})
export class AppUserForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppUserService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly roleOptions = signal<RoleOption[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    userId: ['', Validators.required],
    userName: ['', Validators.required],
    isActive: [true],
    roleIds: [[] as string[]],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!id);

    forkJoin({
      roles: this.lookups.appRoles(),
      user: id ? this.service.get(id) : of(null),
    }).subscribe({
      next: ({ roles, user }) => {
        this.roleOptions.set(this.toOptions(roles));
        if (user) {
          this.form.patchValue({
            userId: user.userId,
            userName: user.userName,
            isActive: user.isActive,
            roleIds: user.roleIds ?? [],
          });
          this.form.controls.userId.disable(); // UserId is the immutable PK
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入表單資料' });
      },
    });
  }

  private toOptions(roles: AppRoleLookup[]): RoleOption[] {
    return roles.map((r) => ({ roleId: r.roleId, label: `${r.roleName} (${r.roleId})` }));
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue(); // includes disabled userId
    const request: AppUserRequest = {
      userId: value.userId,
      userName: value.userName,
      isActive: value.isActive,
      roleIds: value.roleIds,
    };

    const call = this.isEdit() ? this.service.update(request) : this.service.create(request);
    call.subscribe({
      next: (user) => {
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `使用者「${user.userName}」已儲存` });
        this.router.navigate(['/app-users', user.userId]);
      },
      error: (err) => {
        this.saving.set(false);
        const detail = err?.status === 409 ? '帳號已存在' : '儲存失敗';
        this.messages.add({ severity: 'error', summary: '錯誤', detail });
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/app-users']);
  }

  protected isInvalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.dirty || c.touched);
  }
}
