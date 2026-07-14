import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';
import { forkJoin } from 'rxjs';
import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole, AppUserLookup } from '@core/models/app-role.model';

/** Read-only detail page for a role (檢視角色). */
@Component({
  selector: 'app-app-role-detail',
  imports: [CommonModule, ButtonModule, ChipModule],
  templateUrl: './app-role-detail.html',
  styleUrl: './app-role-detail.scss',
})
export class AppRoleDetail implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly role = signal<AppRole | null>(null);
  protected readonly userLabels = signal<string[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    forkJoin({
      role: this.service.get(id),
      users: this.lookups.appUsers(),
    }).subscribe({
      next: ({ role, users }) => {
        this.role.set(role);
        this.userLabels.set(this.mapUserLabels(role.userIds, users));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private mapUserLabels(userIds: string[], users: AppUserLookup[]): string[] {
    const byId = new Map(users.map((u) => [u.userId, u.userName]));
    return userIds.map((id) => {
      const name = byId.get(id);
      return name ? `${name} (${id})` : id;
    });
  }

  protected back(): void {
    this.router.navigate(['/app-roles']);
  }

  protected edit(): void {
    const role = this.role();
    if (role) {
      this.router.navigate(['/app-roles', role.roleId, 'edit']);
    }
  }
}
