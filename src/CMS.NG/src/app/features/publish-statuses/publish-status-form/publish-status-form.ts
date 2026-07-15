import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatusRequest } from '@core/models/publish-status.model';
import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

/** Add / edit form for a publishing status (新增／編輯發布狀態). */
@Component({
  selector: 'app-publish-status-form',
  imports: [
    CommonModule, ReactiveFormsModule, ButtonModule,
    InputTextModule, InputNumberModule, CheckboxModule, RowAuditBadge,
  ],
  templateUrl: './publish-status-form.html',
  styleUrl: './publish-status-form.scss',
})
export class PublishStatusForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  // Numeric pkid of the loaded record, for the Row Audit badge (edit mode only).
  protected readonly recordPkid = signal<number | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    pkid: [null as number | null, Validators.required],
    description: ['', Validators.required],
    isDraft: [false],
    isPublished: [false],
    isDiscontinued: [false],
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!idParam);

    if (idParam) {
      this.service.get(Number(idParam)).subscribe({
        next: (status) => {
          this.recordPkid.set(status.pkid);
          this.form.patchValue({
            pkid: status.pkid,
            description: status.description,
            isDraft: status.isDraft,
            isPublished: status.isPublished,
            isDiscontinued: status.isDiscontinued,
          });
          this.form.controls.pkid.disable(); // pkid is the immutable PK
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
    const value = this.form.getRawValue(); // includes disabled pkid
    const request: PublishStatusRequest = {
      pkid: value.pkid!,
      description: value.description.trim(),
      isDraft: value.isDraft,
      isPublished: value.isPublished,
      isDiscontinued: value.isDiscontinued,
    };

    const call = this.isEdit() ? this.service.update(request) : this.service.create(request);
    call.subscribe({
      next: (status) => {
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `發布狀態「${status.description}」已儲存` });
        this.router.navigate(['/publish-statuses', status.pkid]);
      },
      error: (err) => {
        this.saving.set(false);
        const detail = err?.status === 409 ? '主代碼已存在' : '儲存失敗';
        this.messages.add({ severity: 'error', summary: '錯誤', detail });
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/publish-statuses']);
  }

  protected isInvalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.dirty || c.touched);
  }
}
