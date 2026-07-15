import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseQuery } from '@core/models/course.model';

const FILTERS_KEY = 'course-list-filters';
const SORT_KEY = 'course-list-sort';
const PAGE_KEY = 'course-list-page';

interface SortState { sortField: string | null; sortOrder: number; }
interface PageState { first: number; rows: number; }
interface SelectOption { label: string; value: number | null; }
interface TriOption { label: string; value: boolean | null; }
interface DateRangeModel {
  scheduleOnFrom: Date | null;
  scheduleOnTo: Date | null;
  scheduleOffFrom: Date | null;
  scheduleOffTo: Date | null;
}

/** Course list page (課程) — sortable/paginated table with a filter drawer. */
@Component({
  selector: 'app-course-list',
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule, DrawerModule,
    InputTextModule, SelectModule, DatePickerModule, TagModule, TooltipModule,
  ],
  templateUrl: './course-list.html',
  styleUrl: './course-list.scss',
})
export class CourseList implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookups = inject(LookupService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  protected readonly courses = signal<Course[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterVisible = signal(false);

  // Filter-drawer select options (loaded from lookups).
  protected readonly partnerOptions = signal<SelectOption[]>([]);
  protected readonly courseGroupOptions = signal<SelectOption[]>([]);
  protected readonly publishStatusOptions = signal<SelectOption[]>([]);

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

  protected filter: CourseQuery = this.loadFilters();
  protected readonly sort: SortState = this.loadSort();
  protected readonly page: PageState = this.loadPage();

  // p-datepicker binds Date; the query carries ISO strings.
  protected dateModel: DateRangeModel = {
    scheduleOnFrom: fromIso(this.filter.scheduleOnFrom),
    scheduleOnTo: fromIso(this.filter.scheduleOnTo),
    scheduleOffFrom: fromIso(this.filter.scheduleOffFrom),
    scheduleOffTo: fromIso(this.filter.scheduleOffTo),
  };

  ngOnInit(): void {
    this.loadLookups();
    this.search();
  }

  private loadLookups(): void {
    forkJoin({
      partners: this.lookups.partners(),
      courseGroups: this.lookups.courseGroups(),
      publishStatuses: this.lookups.publishStatuses(),
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses }) => {
        this.partnerOptions.set(partners.map((p) => ({ label: p.name, value: p.pkid })));
        this.courseGroupOptions.set(courseGroups.map((g) => ({ label: g.description, value: g.pkid })));
        this.publishStatusOptions.set(publishStatuses.map((s) => ({ label: s.description, value: s.pkid })));
      },
      error: () => this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入篩選選項' }),
    });
  }

  /** Runs the current filter against the API. */
  protected search(): void {
    this.loading.set(true);
    const call = this.hasActiveFilter() ? this.service.query(this.filter) : this.service.list();
    call.subscribe({
      next: (rows) => {
        this.courses.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入課程資料' });
      },
    });
  }

  private hasActiveFilter(): boolean {
    const f = this.filter;
    return !!f.keyword
      || f.partnerPkid != null
      || f.courseGroupPkid != null
      || f.publishStatusPkid != null
      || !!f.scheduleOnFrom || !!f.scheduleOnTo
      || !!f.scheduleOffFrom || !!f.scheduleOffTo
      || f.canRepeat != null;
  }

  protected applyFilter(): void {
    this.filter.scheduleOnFrom = toIso(this.dateModel.scheduleOnFrom);
    this.filter.scheduleOnTo = toIso(this.dateModel.scheduleOnTo);
    this.filter.scheduleOffFrom = toIso(this.dateModel.scheduleOffFrom);
    this.filter.scheduleOffTo = toIso(this.dateModel.scheduleOffTo);
    this.persistFilters();
    this.page.first = 0;
    this.persistPage();
    this.filterVisible.set(false);
    this.search();
  }

  protected resetFilter(): void {
    this.filter = {
      keyword: null, partnerPkid: null, courseGroupPkid: null, publishStatusPkid: null,
      scheduleOnFrom: null, scheduleOnTo: null, scheduleOffFrom: null, scheduleOffTo: null,
      canRepeat: null,
    };
    this.dateModel = {
      scheduleOnFrom: null, scheduleOnTo: null, scheduleOffFrom: null, scheduleOffTo: null,
    };
    this.persistFilters();
    this.search();
  }

  protected add(): void {
    this.router.navigate(['/courses/new']);
  }

  protected view(course: Course): void {
    this.router.navigate(['/courses', course.pkid]);
  }

  protected edit(course: Course): void {
    this.router.navigate(['/courses', course.pkid, 'edit']);
  }

  protected confirmDelete(course: Course): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${course.pkid}</b>「${course.courseId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(course),
    });
  }

  private delete(course: Course): void {
    this.service.delete(course.pkid).subscribe({
      next: () => {
        this.messages.add({ severity: 'success', summary: '已刪除', detail: `課程「${course.title}」已刪除` });
        this.search();
      },
      error: () => this.messages.add({ severity: 'error', summary: '刪除失敗', detail: '無法刪除課程' }),
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

  private loadFilters(): CourseQuery {
    return this.read<CourseQuery>(FILTERS_KEY, {
      keyword: null, partnerPkid: null, courseGroupPkid: null, publishStatusPkid: null,
      scheduleOnFrom: null, scheduleOnTo: null, scheduleOffFrom: null, scheduleOffTo: null,
      canRepeat: null,
    });
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

/** Converts a local Date to an ISO yyyy-MM-dd string (local parts), or null. */
function toIso(date: Date | null): string | null {
  if (!date) {
    return null;
  }
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Parses an ISO yyyy-MM-dd string into a local Date (midnight), or null. */
function fromIso(iso: string | null | undefined): Date | null {
  return iso ? new Date(`${iso}T00:00:00`) : null;
}
