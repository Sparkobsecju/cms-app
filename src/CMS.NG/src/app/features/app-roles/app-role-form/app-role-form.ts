import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MultiSelectModule } from 'primeng/multiselect';
import { MessageService } from 'primeng/api';
import { forkJoin, of } from 'rxjs';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRoleRequest, AppUserLookup } from '@core/models/app-role.model';
import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

interface UserOption { userId: string; label: string; }

/** Add / edit form for a role (新增／編輯角色). */
@Component({
  selector: 'app-app-role-form',
  imports: [
    CommonModule, ReactiveFormsModule, ButtonModule,
    InputTextModule, InputNumberModule, MultiSelectModule, RowAuditBadge,
  ],
  templateUrl: './app-role-form.html',
  styleUrl: './app-role-form.scss',
})
export class AppRoleForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppRoleService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly userOptions = signal<UserOption[]>([]);
  // Numeric pkid of the loaded record, for the Row Audit badge (edit mode only).
  protected readonly recordPkid = signal<number | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    roleId: ['', Validators.required],
    roleName: ['', Validators.required],
    permissionLevel: [100, Validators.required],
    description: [''],
    userIds: [[] as string[]],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!id);

    forkJoin({
      users: this.lookups.appUsers(),
      role: id ? this.service.get(id) : of(null),
    }).subscribe({
      next: ({ users, role }) => {
        this.userOptions.set(this.toOptions(users));
        if (role) {
          this.recordPkid.set(role.pkid);
          this.form.patchValue({
            roleId: role.roleId,
            roleName: role.roleName,
            permissionLevel: role.permissionLevel,
            description: role.description ?? '',
            userIds: role.userIds ?? [],
          });
          this.form.controls.roleId.disable(); // RoleId is the immutable PK
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入表單資料' });
      },
    });
  }

  private toOptions(users: AppUserLookup[]): UserOption[] {
    return users.map((u) => ({ userId: u.userId, label: `${u.userName} (${u.userId})` }));
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue(); // includes disabled roleId
    const request: AppRoleRequest = {
      roleId: value.roleId,
      roleName: value.roleName,
      permissionLevel: value.permissionLevel,
      description: value.description?.trim() ? value.description.trim() : null,
      userIds: value.userIds,
    };

    const call = this.isEdit() ? this.service.update(request) : this.service.create(request);
    call.subscribe({
      next: (role) => {
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `角色「${role.roleName}」已儲存` });
        this.router.navigate(['/app-roles', role.roleId]);
      },
      error: (err) => {
        this.saving.set(false);
        const detail = err?.status === 409 ? '角色代碼已存在' : '儲存失敗';
        this.messages.add({ severity: 'error', summary: '錯誤', detail });
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/app-roles']);
  }

  protected isInvalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.dirty || c.touched);
  }
}
