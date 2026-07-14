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
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus, PublishStatusQuery } from '@core/models/publish-status.model';

const FILTERS_KEY = 'publish-status-list-filters';
const SORT_KEY = 'publish-status-list-sort';
const PAGE_KEY = 'publish-status-list-page';

interface SortState { sortField: string | null; sortOrder: number; }
interface PageState { first: number; rows: number; }
interface TriOption { label: string; value: boolean | null; }

/** PublishStatus list page (發布狀態) — sortable/paginated table with a filter drawer. */
@Component({
  selector: 'app-publish-status-list',
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule, DrawerModule,
    InputTextModule, SelectModule, TagModule, TooltipModule,
  ],
  templateUrl: './publish-status-list.html',
  styleUrl: './publish-status-list.scss',
})
export class PublishStatusList implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly statuses = signal<PublishStatus[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  /** Tri-state options for the bit-flag filters. */
  protected readonly triOptions: TriOption[] = [
    { label: '不限', value: null },
    { label: '是', value: true },
    { label: '否', value: false },
  ];

  /** Two-way bridge for p-drawer [(visible)]. */
  protected get filterVisibleModel(): boolean {
    return this.filterVisible();
  }
  protected set filterVisibleModel(value: boolean) {
    this.filterVisible.set(value);
  }

  protected filter: PublishStatusQuery = this.loadFilters();
  protected readonly sort: SortState = this.loadSort();
  protected readonly page: PageState = this.loadPage();

  ngOnInit(): void {
    this.search();
  }

  /** Runs the current filter against the API. */
  protected search(): void {
    this.loading.set(true);
    const hasFilter = !!this.filter.keyword
      || this.filter.isDraft != null
      || this.filter.isPublished != null
      || this.filter.isDiscontinued != null;
    const call = hasFilter ? this.service.query(this.filter) : this.service.list();
    call.subscribe({
      next: (rows) => {
        this.statuses.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入發布狀態資料' });
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
    this.filter = { keyword: null, isDraft: null, isPublished: null, isDiscontinued: null };
    this.persistFilters();
    this.search();
  }

  protected add(): void {
    this.router.navigate(['/publish-statuses/new']);
  }

  protected view(status: PublishStatus): void {
    this.router.navigate(['/publish-statuses', status.pkid]);
  }

  protected edit(status: PublishStatus): void {
    this.router.navigate(['/publish-statuses', status.pkid, 'edit']);
  }

  protected confirmDelete(status: PublishStatus): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${status.pkid}</b>「${status.description}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(status),
    });
  }

  private delete(status: PublishStatus): void {
    this.service.delete(status.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `發布狀態「${status.description}」已刪除` });
        this.search();
      },
      error: () => this.messages.add({ severity: 'error', summary: '刪除失敗', detail: '無法刪除發布狀態' }),
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

  private loadFilters(): PublishStatusQuery {
    return this.read<PublishStatusQuery>(FILTERS_KEY, {
      keyword: null, isDraft: null, isPublished: null, isDiscontinued: null,
    });
  }
  private persistFilters(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filter));
  }
  private loadSort(): SortState {
    return this.read<SortState>(SORT_KEY, { sortField: 'pkid', sortOrder: 1 });
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
