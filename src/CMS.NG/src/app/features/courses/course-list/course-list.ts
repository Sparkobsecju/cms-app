import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { AutoFocus } from 'primeng/autofocus';
import { ConfirmationService, MessageService } from 'primeng/api';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseQuery, CourseRequest } from '@core/models/course.model';

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

/** Editor widget kind used by an inline-editable column. */
type EditorType = 'text' | 'number' | 'date' | 'select' | 'checkbox';
/** Course fields the list table allows editing in place (everything except pkid + the two FK lookups). */
type EditField =
  | 'displayOrder' | 'courseId' | 'prodCourseId' | 'title' | 'publishStatusPkid'
  | 'scheduleOn' | 'scheduleOff' | 'hour' | 'listPrice' | 'learningCredit' | 'canRepeat';
/** Value bound to the active cell editor. */
type EditValue = string | number | boolean | Date | null;

/** Editable columns → the editor type that matches each. Read-only columns (pkid, partnerName,
 *  courseGroupDescription) are deliberately absent, so {@link CourseList.isEditable} rejects them. */
const EDITABLE_COLUMNS: Record<EditField, EditorType> = {
  displayOrder: 'number',
  courseId: 'text',
  prodCourseId: 'text',
  title: 'text',
  publishStatusPkid: 'select',
  scheduleOn: 'date',
  scheduleOff: 'date',
  hour: 'number',
  listPrice: 'number',
  learningCredit: 'number',
  canRepeat: 'checkbox',
};

interface EditingCell { pkid: number; field: EditField; }

/** Course list page (課程) — sortable/paginated table with a filter drawer. */
@Component({
  selector: 'app-course-list',
  imports: [
    CommonModule, FormsModule, TableModule, ButtonModule, DrawerModule,
    InputTextModule, InputNumberModule, SelectModule, DatePickerModule, CheckboxModule,
    TagModule, TooltipModule, AutoFocus,
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

  // ----- inline (in-place) cell editing -----
  /** The cell currently in edit mode, or null when nothing is being edited. */
  protected readonly editing = signal<EditingCell | null>(null);
  /** Working value bound to the active editor (two-way via ngModel). */
  protected editValue: EditValue = null;
  /** Inline validation message for the active cell; keeps the cell open while set. */
  protected readonly editError = signal<string | null>(null);
  /** True while a row's change is being persisted (blocks re-entering edit mid-save). */
  protected readonly rowSaving = signal(false);

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

  // ----- inline editing -----

  /** Whether a column may be edited in place (false for pkid + the two FK-lookup columns). */
  protected isEditable(field: string): field is EditField {
    return Object.prototype.hasOwnProperty.call(EDITABLE_COLUMNS, field);
  }

  /** The editor widget type for a column (used by the template's @switch). */
  protected editorType(field: EditField): EditorType {
    return EDITABLE_COLUMNS[field];
  }

  /** True when this exact cell (row + column) is the one currently open for editing. */
  protected isEditing(pkid: number, field: EditField): boolean {
    const e = this.editing();
    return !!e && e.pkid === pkid && e.field === field;
  }

  /** Enter edit mode for a cell. Only fired from double-click on editable columns. */
  protected startEdit(course: Course, field: string): void {
    if (this.rowSaving() || !this.isEditable(field)) {
      return;
    }
    this.editError.set(null);
    this.editValue = this.toEditorValue(course, field);
    this.editing.set({ pkid: course.pkid, field });
  }

  /** Leave edit mode without saving (Escape / abandon). The row keeps its previous value. */
  protected cancelEdit(): void {
    this.editing.set(null);
    this.editError.set(null);
  }

  /**
   * Blur handler for the overlay editors (select / date). Those commit on their value-change
   * events, so blur only needs to *close* the editor when the user clicks away without changing
   * anything. Deferred to a macrotask so any pending change event fires first, and it never fights
   * an in-flight save or a validation error.
   */
  protected closeOnBlur(): void {
    const cell = this.editing();
    if (!cell) {
      return;
    }
    setTimeout(() => {
      const current = this.editing();
      const sameCell = current && current.pkid === cell.pkid && current.field === cell.field;
      if (sameCell && !this.rowSaving() && !this.editError()) {
        this.cancelEdit();
      }
    });
  }

  /** Validate then persist the active cell on blur. Invalid → inline error, stay in edit mode. */
  protected commitEdit(): void {
    const state = this.editing();
    if (!state || this.rowSaving()) {
      return; // nothing open, or a save is already in flight for this row
    }
    const course = this.courses().find((c) => c.pkid === state.pkid);
    if (!course) {
      this.cancelEdit();
      return;
    }

    const error = this.validate(course, state.field, this.editValue);
    if (error) {
      this.editError.set(error); // keep the cell open so the user can fix it
      return;
    }

    const newValue = this.normalize(state.field, this.editValue);
    if (course[state.field] === newValue) {
      this.cancelEdit(); // unchanged — nothing to persist
      return;
    }

    this.persist(course, state.field, newValue);
  }

  /**
   * Save a single edited field. The list projection omits the N-N pkid arrays, so a blind PUT
   * would wipe the course's certifications/job categories — instead we GET the full course,
   * overlay just the edited field, then PUT. The PUT returns the fully-joined row to display.
   */
  private persist(course: Course, field: EditField, newValue: string | number | boolean): void {
    this.rowSaving.set(true);
    this.service.get(course.pkid).subscribe({
      next: (full) => {
        const request = toRequest({ ...full, [field]: newValue });
        this.service.update(request).subscribe({
          next: (saved) => {
            this.courses.update((rows) => rows.map((r) => (r.pkid === saved.pkid ? saved : r)));
            this.rowSaving.set(false);
            this.cancelEdit();
            this.messages.add({ severity: 'success', summary: '已更新', detail: `課程「${saved.title}」已更新` });
          },
          error: () => this.revertEdit(),
        });
      },
      error: () => this.revertEdit(),
    });
  }

  /** Roll back a failed save: the row signal was never mutated, so just close and surface the error. */
  private revertEdit(): void {
    this.rowSaving.set(false);
    this.cancelEdit();
    this.messages.add({ severity: 'error', summary: '更新失敗', detail: '無法儲存變更，已還原為原值' });
  }

  /** Seed the editor from the row: date columns need a Date, everything else the raw value. */
  private toEditorValue(course: Course, field: EditField): EditValue {
    if (field === 'scheduleOn' || field === 'scheduleOff') {
      return fromIso(course[field]);
    }
    return course[field] as EditValue;
  }

  /** Normalise the editor value for persistence (trim text, Date→ISO); numbers/bools pass through. */
  private normalize(field: EditField, value: EditValue): string | number | boolean {
    switch (EDITABLE_COLUMNS[field]) {
      case 'text':
        return String(value ?? '').trim();
      case 'date':
        return toIso(value as Date)!;
      default:
        return value as number | boolean;
    }
  }

  /** Field-specific validation; returns a Chinese error message or null when the value is valid. */
  private validate(course: Course, field: EditField, value: EditValue): string | null {
    switch (field) {
      case 'title':
      case 'courseId':
      case 'prodCourseId':
        return isBlank(value) ? '此欄位為必填，不可清空' : null;
      case 'displayOrder':
        return isValidNumber(value) ? null : '請輸入有效數字';
      case 'hour':
      case 'listPrice':
      case 'learningCredit':
        if (!isValidNumber(value)) {
          return '請輸入有效數字';
        }
        return (value as number) < 0 ? '不可為負數' : null;
      case 'publishStatusPkid':
        return value == null ? '請選擇上架狀態' : null;
      case 'scheduleOn': {
        if (!isValidDate(value)) {
          return '請輸入有效日期';
        }
        const off = fromIso(course.scheduleOff);
        return off && (value as Date) > off ? '上架日期不可晚於下架日期' : null;
      }
      case 'scheduleOff': {
        if (!isValidDate(value)) {
          return '請輸入有效日期';
        }
        const on = fromIso(course.scheduleOn);
        return on && (value as Date) < on ? '下架日期不可早於上架日期' : null;
      }
      case 'canRepeat':
        return null;
    }
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

/** True when a required text value is null/blank (whitespace-only counts as blank). */
function isBlank(value: EditValue): boolean {
  return value == null || (typeof value === 'string' && value.trim() === '');
}

/** True for a finite number (p-inputNumber yields null when cleared). */
function isValidNumber(value: EditValue): boolean {
  return typeof value === 'number' && Number.isFinite(value);
}

/** True for a valid Date instance (rejects null and Invalid Date). */
function isValidDate(value: EditValue): boolean {
  return value instanceof Date && !Number.isNaN(value.getTime());
}

/** Maps a full Course (from GET-by-id, incl. N-N arrays) to the PUT request body. */
function toRequest(c: Course): CourseRequest {
  return {
    pkid: c.pkid,
    title: c.title,
    officialTitle: c.officialTitle ?? null,
    courseId: c.courseId,
    prodCourseId: c.prodCourseId,
    friendlyUrl: c.friendlyUrl,
    displayOrder: c.displayOrder,
    partnerPkid: c.partnerPkid,
    courseGroupPkid: c.courseGroupPkid ?? null,
    publishStatusPkid: c.publishStatusPkid,
    scheduleOn: c.scheduleOn,
    scheduleOff: c.scheduleOff,
    hour: c.hour,
    listPrice: c.listPrice,
    learningCredit: c.learningCredit,
    material: c.material ?? null,
    objective: c.objective ?? null,
    target: c.target ?? null,
    prerequisites: c.prerequisites ?? null,
    outline: c.outline ?? null,
    towardCertOrExam: c.towardCertOrExam ?? null,
    note: c.note ?? null,
    otherInfo: c.otherInfo ?? null,
    canRepeat: c.canRepeat,
    certificationPkids: c.certificationPkids,
    jobCategoryPkids: c.jobCategoryPkids,
  };
}
