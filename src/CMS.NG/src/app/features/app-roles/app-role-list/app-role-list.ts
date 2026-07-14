import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AppRoleService } from '@core/services/app-role.service';
import { AppRole, AppRoleQuery } from '@core/models/app-role.model';

const FILTERS_KEY = 'app-role-list-filters';
const SORT_KEY = 'app-role-list-sort';
const PAGE_KEY = 'app-role-list-page';

interface SortState { sortField: string | null; sortOrder: number; }
interface PageState { first: number; rows: number; }

/** Roles list page (角色 AppRole) — sortable/paginated table with a filter drawer. */
@Component({
  selector: 'app-app-role-list',
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule, DrawerModule,
    InputTextModule, InputNumberModule, TooltipModule,
  ],
  templateUrl: './app-role-list.html',
  styleUrl: './app-role-list.scss',
})
export class AppRoleList implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly roles = signal<AppRole[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  /** Two-way bridge for p-drawer [(visible)]. */
  protected get filterVisibleModel(): boolean {
    return this.filterVisible();
  }
  protected set filterVisibleModel(value: boolean) {
    this.filterVisible.set(value);
  }

  protected filter: AppRoleQuery = this.loadFilters();
  protected readonly sort: SortState = this.loadSort();
  protected readonly page: PageState = this.loadPage();

  ngOnInit(): void {
    this.search();
  }

  /** Runs the current filter against the API. */
  protected search(): void {
    this.loading.set(true);
    const hasFilter = !!this.filter.keyword || this.filter.permissionLevel != null;
    const call = hasFilter ? this.service.query(this.filter) : this.service.list();
    call.subscribe({
      next: (rows) => {
        this.roles.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入角色資料' });
      },
    });
  }

  protected applyFilter(): void {
    this.persistFilters();
    this.page.first = 0;
    this.persistPage();
    this.filterVisible.set(false);
    this.search();
  }

  protected resetFilter(): void {
    this.filter = { keyword: null, permissionLevel: null };
    this.persistFilters();
    this.search();
  }

  protected add(): void {
    this.router.navigate(['/app-roles/new']);
  }

  protected view(role: AppRole): void {
    this.router.navigate(['/app-roles', role.roleId]);
  }

  protected edit(role: AppRole): void {
    this.router.navigate(['/app-roles', role.roleId, 'edit']);
  }

  protected confirmDelete(role: AppRole): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除角色 <b>${role.roleId}</b>「${role.roleName}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(role),
    });
  }

  private delete(role: AppRole): void {
    this.service.delete(role.roleId).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `角色「${role.roleName}」已刪除` });
        this.search();
      },
      error: () => this.messages.add({ severity: 'error', summary: '刪除失敗', detail: '無法刪除角色' }),
    });
  }

  protected onSort(event: { field?: string | null; order?: number }): void {
    this.sort.sortField = event.field ?? null;
    this.sort.sortOrder = event.order ?? 1;
    this.persistSort();
  }

  protected onPage(event: TableLazyLoadEvent): void {
    this.page.first = event.first ?? 0;
    this.page.rows = event.rows ?? 20;
    this.persistPage();
  }

  // ----- session storage helpers -----

  private loadFilters(): AppRoleQuery {
    return this.read<AppRoleQuery>(FILTERS_KEY, { keyword: null, permissionLevel: null });
  }
  private persistFilters(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filter));
  }
  private loadSort(): SortState {
    return this.read<SortState>(SORT_KEY, { sortField: 'roleId', sortOrder: 1 });
  }
  private persistSort(): void {
    sessionStorage.setItem(SORT_KEY, JSON.stringify(this.sort));
  }
  private loadPage(): PageState {
    return this.read<PageState>(PAGE_KEY, { first: 0, rows: 20 });
  }
  private persistPage(): void {
    sessionStorage.setItem(PAGE_KEY, JSON.stringify(this.page));
  }
  private read<T>(key: string, fallback: T): T {
    try {
      const raw = sessionStorage.getItem(key);
      return raw ? { ...fallback, ...JSON.parse(raw) } : fallback;
    } catch {
      return fallback;
    }
  }
}
