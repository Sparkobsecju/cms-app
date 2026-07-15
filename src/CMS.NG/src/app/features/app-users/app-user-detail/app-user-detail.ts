import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';
import { TagModule } from 'primeng/tag';
import { ConfirmationService, MessageService } from 'primeng/api';
import { forkJoin } from 'rxjs';
import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser, AppRoleLookup } from '@core/models/app-user.model';

/** Read-only detail page for a user (檢視使用者). */
@Component({
  selector: 'app-app-user-detail',
  imports: [CommonModule, ButtonModule, ChipModule, TagModule],
  templateUrl: './app-user-detail.html',
  styleUrl: './app-user-detail.scss',
})
export class AppUserDetail implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly user = signal<AppUser | null>(null);
  protected readonly roleLabels = signal<string[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    forkJoin({
      user: this.service.get(id),
      roles: this.lookups.appRoles(),
    }).subscribe({
      next: ({ user, roles }) => {
        this.user.set(user);
        this.roleLabels.set(this.mapRoleLabels(user.roleIds, roles));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private mapRoleLabels(roleIds: string[], roles: AppRoleLookup[]): string[] {
    const byId = new Map(roles.map((r) => [r.roleId, r.roleName]));
    return roleIds.map((id) => {
      const name = byId.get(id);
      return name ? `${name} (${id})` : id;
    });
  }

  protected confirmResetPassword(): void {
    const user = this.user();
    if (!user) {
      return;
    }
    this.confirm.confirm({
      header: '重設密碼',
      message: `確定要將「${user.userName}」的密碼重設為預設密碼？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '重設',
      rejectLabel: '取消',
      accept: () => this.resetPassword(user),
    });
  }

  private resetPassword(user: AppUser): void {
    this.service.resetPassword(user.userId).subscribe({
      next: () => this.messages.add({ severity: 'success', summary: '已重設', detail: `使用者「${user.userName}」的密碼已重設為預設密碼` }),
      error: () => this.messages.add({ severity: 'error', summary: '重設失敗', detail: '無法重設密碼' }),
    });
  }

  protected back(): void {
    this.router.navigate(['/app-users']);
  }

  protected edit(): void {
    const user = this.user();
    if (user) {
      this.router.navigate(['/app-users', user.userId, 'edit']);
    }
  }
}
