import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup, CourseGroupQuery } from '@core/models/course-group.model';

const FILTERS_KEY = 'course-group-list-filters';
const SORT_KEY = 'course-group-list-sort';
const PAGE_KEY = 'course-group-list-page';

interface SortState { sortField: string | null; sortOrder: number; }
interface PageState { first: number; rows: number; }

/** CourseGroup list page (課程群組) — sortable/paginated table with a filter drawer. */
@Component({
  selector: 'app-course-group-list',
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule, DrawerModule,
    InputTextModule, TooltipModule,
  ],
  templateUrl: './course-group-list.html',
  styleUrl: './course-group-list.scss',
})
export class CourseGroupList implements OnInit {
  private readonly service = inject(CourseGroupService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly groups = signal<CourseGroup[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  /** Two-way bridge for p-drawer [(visible)]. */
  protected get filterVisibleModel(): boolean {
    return this.filterVisible();
  }
  protected set filterVisibleModel(value: boolean) {
    this.filterVisible.set(value);
  }

  protected filter: CourseGroupQuery = this.loadFilters();
  protected readonly sort: SortState = this.loadSort();
  protected readonly page: PageState = this.loadPage();

  ngOnInit(): void {
    this.search();
  }

  /** Runs the current filter against the API. */
  protected search(): void {
    this.loading.set(true);
    const hasFilter = !!this.filter.keyword;
    const call = hasFilter ? this.service.query(this.filter) : this.service.list();
    call.subscribe({
      next: (rows) => {
        this.groups.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入課程群組資料' });
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
    this.filter = { keyword: null };
    this.persistFilters();
    this.search();
  }

  protected add(): void {
    this.router.navigate(['/course-groups/new']);
  }

  protected view(group: CourseGroup): void {
    this.router.navigate(['/course-groups', group.pkid]);
  }

  protected edit(group: CourseGroup): void {
    this.router.navigate(['/course-groups', group.pkid, 'edit']);
  }

  protected confirmDelete(group: CourseGroup): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${group.pkid}</b>「${group.description}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(group),
    });
  }

  private delete(group: CourseGroup): void {
    this.service.delete(group.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `課程群組「${group.description}」已刪除` });
        this.search();
      },
      error: () => this.messages.add({ severity: 'error', summary: '刪除失敗', detail: '無法刪除課程群組' }),
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

  private loadFilters(): CourseGroupQuery {
    return this.read<CourseGroupQuery>(FILTERS_KEY, { keyword: null });
  }
  private persistFilters(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filter));
  }
  private loadSort(): SortState {
    return this.read<SortState>(SORT_KEY, { sortField: 'pkid', sortOrder: -1 });
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
