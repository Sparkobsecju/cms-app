import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroupRequest } from '@core/models/course-group.model';
import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

/** Add / edit form for a course group (新增／編輯課程群組). */
@Component({
  selector: 'app-course-group-form',
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule, RowAuditBadge],
  templateUrl: './course-group-form.html',
  styleUrl: './course-group-form.scss',
})
export class CourseGroupForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseGroupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  // pkid is smallint IDENTITY: shown read-only in edit mode, absent in add mode.
  protected readonly pkid = signal<number | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    description: ['', Validators.required],
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!idParam);

    if (idParam) {
      this.service.get(Number(idParam)).subscribe({
        next: (group) => {
          this.pkid.set(group.pkid);
          this.form.patchValue({ description: group.description });
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入表單資料' });
        },
      });
    } else {
      this.loading.set(false);
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    const request: CourseGroupRequest = {
      pkid: this.pkid() ?? 0, // ignored by the API on create (IDENTITY)
      description: value.description.trim(),
    };

    const call = this.isEdit() ? this.service.update(request) : this.service.create(request);
    call.subscribe({
      next: (group) => {
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `課程群組「${group.description}」已儲存` });
        this.router.navigate(['/course-groups', group.pkid]);
      },
      error: () => {
        this.saving.set(false);
        this.messages.add({ severity: 'error', summary: '錯誤', detail: '儲存失敗' });
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/course-groups']);
  }

  protected isInvalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.dirty || c.touched);
  }
}
