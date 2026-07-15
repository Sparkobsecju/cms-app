import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { DatePickerModule } from 'primeng/datepicker';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseRequest } from '@core/models/course.model';
import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

interface SelectOption { label: string; value: number | null; }

/** Add / edit form for a course (新增／編輯課程). */
@Component({
  selector: 'app-course-form',
  imports: [
    CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule, InputNumberModule,
    TextareaModule, SelectModule, MultiSelectModule, DatePickerModule, CheckboxModule, RowAuditBadge,
  ],
  templateUrl: './course-form.html',
  styleUrl: './course-form.scss',
})
export class CourseForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseService);
  private readonly lookups = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  // pkid is int IDENTITY: shown read-only in edit mode, absent in add mode.
  protected readonly pkid = signal<number | null>(null);

  protected readonly partnerOptions = signal<SelectOption[]>([]);
  protected readonly courseGroupOptions = signal<SelectOption[]>([]);
  protected readonly publishStatusOptions = signal<SelectOption[]>([]);
  protected readonly certificationOptions = signal<SelectOption[]>([]);
  protected readonly jobCategoryOptions = signal<SelectOption[]>([]);

  protected readonly form = this.fb.group({
    title: this.fb.nonNullable.control('', Validators.required),
    officialTitle: this.fb.control<string | null>(null),
    courseId: this.fb.nonNullable.control('', Validators.required),
    prodCourseId: this.fb.nonNullable.control('', Validators.required),
    friendlyUrl: this.fb.nonNullable.control('', Validators.required),
    displayOrder: this.fb.nonNullable.control(0, Validators.required),
    partnerPkid: this.fb.control<number | null>(null, Validators.required),
    courseGroupPkid: this.fb.control<number | null>(null),
    publishStatusPkid: this.fb.control<number | null>(null, Validators.required),
    scheduleOn: this.fb.control<Date | null>(null, Validators.required),
    scheduleOff: this.fb.control<Date | null>(null, Validators.required),
    hour: this.fb.nonNullable.control(0, Validators.required),
    listPrice: this.fb.nonNullable.control(0, Validators.required),
    learningCredit: this.fb.nonNullable.control(0, Validators.required),
    material: this.fb.control<string | null>(null),
    objective: this.fb.control<string | null>(null),
    target: this.fb.control<string | null>(null),
    prerequisites: this.fb.control<string | null>(null),
    outline: this.fb.control<string | null>(null),
    towardCertOrExam: this.fb.control<string | null>(null),
    note: this.fb.control<string | null>(null),
    otherInfo: this.fb.control<string | null>(null),
    canRepeat: this.fb.nonNullable.control(false),
    certificationPkids: this.fb.nonNullable.control<number[]>([]),
    jobCategoryPkids: this.fb.nonNullable.control<number[]>([]),
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!idParam);

    forkJoin({
      partners: this.lookups.partners(),
      courseGroups: this.lookups.courseGroups(),
      publishStatuses: this.lookups.publishStatuses(),
      certifications: this.lookups.certifications(),
      jobCategories: this.lookups.jobCategories(),
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses, certifications, jobCategories }) => {
        this.partnerOptions.set(partners.map((p) => ({ label: p.name, value: p.pkid })));
        this.courseGroupOptions.set([
          { label: '（無）', value: null },
          ...courseGroups.map((g) => ({ label: g.description, value: g.pkid })),
        ]);
        this.publishStatusOptions.set(publishStatuses.map((s) => ({ label: s.description, value: s.pkid })));
        this.certificationOptions.set(certifications.map((c) => ({ label: `${c.partnerName} - ${c.title}`, value: c.pkid })));
        this.jobCategoryOptions.set(jobCategories.map((j) => ({ label: j.description, value: j.pkid })));

        if (idParam) {
          this.loadCourse(Number(idParam));
        } else {
          this.loading.set(false);
        }
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入選項資料' });
      },
    });
  }

  private loadCourse(id: number): void {
    this.service.get(id).subscribe({
      next: (course) => {
        this.pkid.set(course.pkid);
        this.form.patchValue({
          title: course.title,
          officialTitle: course.officialTitle ?? null,
          courseId: course.courseId,
          prodCourseId: course.prodCourseId,
          friendlyUrl: course.friendlyUrl,
          displayOrder: course.displayOrder,
          partnerPkid: course.partnerPkid,
          courseGroupPkid: course.courseGroupPkid ?? null,
          publishStatusPkid: course.publishStatusPkid,
          scheduleOn: fromIso(course.scheduleOn),
          scheduleOff: fromIso(course.scheduleOff),
          hour: course.hour,
          listPrice: course.listPrice,
          learningCredit: course.learningCredit,
          material: course.material ?? null,
          objective: course.objective ?? null,
          target: course.target ?? null,
          prerequisites: course.prerequisites ?? null,
          outline: course.outline ?? null,
          towardCertOrExam: course.towardCertOrExam ?? null,
          note: course.note ?? null,
          otherInfo: course.otherInfo ?? null,
          canRepeat: course.canRepeat,
          certificationPkids: course.certificationPkids,
          jobCategoryPkids: course.jobCategoryPkids,
        });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入表單資料' });
      },
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const v = this.form.getRawValue();
    const request: CourseRequest = {
      pkid: this.pkid() ?? 0, // ignored by the API on create (IDENTITY)
      title: v.title.trim(),
      officialTitle: trimOrNull(v.officialTitle),
      courseId: v.courseId.trim(),
      prodCourseId: v.prodCourseId.trim(),
      friendlyUrl: v.friendlyUrl.trim(),
      displayOrder: v.displayOrder,
      partnerPkid: v.partnerPkid!,
      courseGroupPkid: v.courseGroupPkid ?? null,
      publishStatusPkid: v.publishStatusPkid!,
      scheduleOn: toIso(v.scheduleOn)!,
      scheduleOff: toIso(v.scheduleOff)!,
      hour: v.hour,
      listPrice: v.listPrice,
      learningCredit: v.learningCredit,
      material: trimOrNull(v.material),
      objective: trimOrNull(v.objective),
      target: trimOrNull(v.target),
      prerequisites: trimOrNull(v.prerequisites),
      outline: trimOrNull(v.outline),
      towardCertOrExam: trimOrNull(v.towardCertOrExam),
      note: trimOrNull(v.note),
      otherInfo: trimOrNull(v.otherInfo),
      canRepeat: v.canRepeat,
      certificationPkids: v.certificationPkids,
      jobCategoryPkids: v.jobCategoryPkids,
    };

    const call = this.isEdit() ? this.service.update(request) : this.service.create(request);
    call.subscribe({
      next: (course) => {
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `課程「${course.title}」已儲存` });
        this.router.navigate(['/courses', course.pkid]);
      },
      error: () => {
        this.saving.set(false);
        this.messages.add({ severity: 'error', summary: '錯誤', detail: '儲存失敗' });
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/courses']);
  }

  protected isInvalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.dirty || c.touched);
  }
}

/** Trims a string, returning null when empty. */
function trimOrNull(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
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
