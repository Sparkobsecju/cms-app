import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AppUserService } from '@core/services/app-user.service';
import { AppUser, AppUserQuery } from '@core/models/app-user.model';

const FILTERS_KEY = 'appuser-list-filters';
const SORT_KEY = 'appuser-list-sort';
const PAGE_KEY = 'appuser-list-page';

interface SortState { sortField: string | null; sortOrder: number; }
interface PageState { first: number; rows: number; }
interface TriOption { label: string; value: boolean | null; }

/** Users list page (使用者 AppUser) — sortable/paginated table with a filter drawer. */
@Component({
  selector: 'app-app-user-list',
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule, DrawerModule,
    InputTextModule, SelectModule, TagModule, TooltipModule,
  ],
  templateUrl: './app-user-list.html',
  styleUrl: './app-user-list.scss',
})
export class AppUserList implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly users = signal<AppUser[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  protected readonly activeOptions: TriOption[] = [
    { label: '全部', value: null },
    { label: '啟用', value: true },
    { label: '停用', value: false },
  ];

  /** Two-way bridge for p-drawer [(visible)]. */
  protected get filterVisibleModel(): boolean {
    return this.filterVisible();
  }
  protected set filterVisibleModel(value: boolean) {
    this.filterVisible.set(value);
  }

  protected filter: AppUserQuery = this.loadFilters();
  protected readonly sort: SortState = this.loadSort();
  protected readonly page: PageState = this.loadPage();

  ngOnInit(): void {
    this.search();
  }

  /** Runs the current filter against the API. */
  protected search(): void {
    this.loading.set(true);
    const hasFilter = !!this.filter.keyword || this.filter.isActive != null;
    const call = hasFilter ? this.service.query(this.filter) : this.service.list();
    call.subscribe({
      next: (rows) => {
        this.users.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入使用者資料' });
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
    this.filter = { keyword: null, isActive: null };
    this.persistFilters();
    this.search();
  }

  protected add(): void {
    this.router.navigate(['/app-users/new']);
  }

  protected view(user: AppUser): void {
    this.router.navigate(['/app-users', user.userId]);
  }

  protected edit(user: AppUser): void {
    this.router.navigate(['/app-users', user.userId, 'edit']);
  }

  protected confirmDelete(user: AppUser): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${user.pkid}</b>「${user.userName}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(user),
    });
  }

  private delete(user: AppUser): void {
    this.service.delete(user.userId).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `使用者「${user.userName}」已刪除` });
        this.search();
      },
      error: () => this.messages.add({ severity: 'error', summary: '刪除失敗', detail: '無法刪除使用者' }),
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

  private loadFilters(): AppUserQuery {
    return this.read<AppUserQuery>(FILTERS_KEY, { keyword: null, isActive: null });
  }
  private persistFilters(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filter));
  }
  private loadSort(): SortState {
    return this.read<SortState>(SORT_KEY, { sortField: 'userId', sortOrder: 1 });
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
