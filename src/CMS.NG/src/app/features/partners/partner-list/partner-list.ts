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
import { PartnerService } from '@core/services/partner.service';
import { Partner, PartnerQuery } from '@core/models/partner.model';

const FILTERS_KEY = 'partner-list-filters';
const SORT_KEY = 'partner-list-sort';
const PAGE_KEY = 'partner-list-page';

interface SortState { sortField: string | null; sortOrder: number; }
interface PageState { first: number; rows: number; }

/** Partner list page (原廠) — sortable/paginated table with a filter drawer. */
@Component({
  selector: 'app-partner-list',
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule, DrawerModule,
    InputTextModule, TooltipModule,
  ],
  templateUrl: './partner-list.html',
  styleUrl: './partner-list.scss',
})
export class PartnerList implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly partners = signal<Partner[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  /** Two-way bridge for p-drawer [(visible)]. */
  protected get filterVisibleModel(): boolean {
    return this.filterVisible();
  }
  protected set filterVisibleModel(value: boolean) {
    this.filterVisible.set(value);
  }

  protected filter: PartnerQuery = this.loadFilters();
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
        this.partners.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入原廠資料' });
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
    this.router.navigate(['/partners/new']);
  }

  protected view(partner: Partner): void {
    this.router.navigate(['/partners', partner.pkid]);
  }

  protected edit(partner: Partner): void {
    this.router.navigate(['/partners', partner.pkid, 'edit']);
  }

  protected viewCourses(partner: Partner): void {
    this.router.navigate(['/courses'], { queryParams: { partnerPkid: partner.pkid } });
  }

  protected confirmDelete(partner: Partner): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${partner.pkid}</b>「${partner.name}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(partner),
    });
  }

  private delete(partner: Partner): void {
    this.service.delete(partner.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `原廠「${partner.name}」已刪除` });
        this.search();
      },
      error: () => this.messages.add({ severity: 'error', summary: '刪除失敗', detail: '無法刪除原廠（可能仍有課程或認證使用）' }),
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

  private loadFilters(): PartnerQuery {
    return this.read<PartnerQuery>(FILTERS_KEY, { keyword: null });
  }
  private persistFilters(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filter));
  }
  private loadSort(): SortState {
    return this.read<SortState>(SORT_KEY, { sortField: 'displayOrder', sortOrder: 1 });
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
