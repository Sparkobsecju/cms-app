import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { PartnerService } from '@core/services/partner.service';
import { PartnerRequest } from '@core/models/partner.model';

/** Add / edit form for a partner (新增／編輯原廠). */
@Component({
  selector: 'app-partner-form',
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './partner-form.html',
  styleUrl: './partner-form.scss',
})
export class PartnerForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  // pkid is smallint IDENTITY: shown read-only in edit mode, absent in add mode.
  protected readonly pkid = signal<number | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    appKey: ['', Validators.required],
    nameOnPartnerMenu: ['', Validators.required],
    nameOnCourseDetailPage: ['', Validators.required],
    displayOrder: [0, Validators.required],
    imageFilename: [''],
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!idParam);

    if (idParam) {
      this.service.get(Number(idParam)).subscribe({
        next: (partner) => {
          this.pkid.set(partner.pkid);
          this.form.patchValue({
            name: partner.name,
            appKey: partner.appKey,
            nameOnPartnerMenu: partner.nameOnPartnerMenu,
            nameOnCourseDetailPage: partner.nameOnCourseDetailPage,
            displayOrder: partner.displayOrder,
            imageFilename: partner.imageFilename ?? '',
          });
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
    const imageFilename = value.imageFilename.trim();
    const request: PartnerRequest = {
      pkid: this.pkid() ?? 0, // ignored by the API on create (IDENTITY)
      name: value.name.trim(),
      appKey: value.appKey.trim(),
      nameOnPartnerMenu: value.nameOnPartnerMenu.trim(),
      nameOnCourseDetailPage: value.nameOnCourseDetailPage.trim(),
      displayOrder: value.displayOrder,
      imageFilename: imageFilename === '' ? null : imageFilename,
    };

    const call = this.isEdit() ? this.service.update(request) : this.service.create(request);
    call.subscribe({
      next: (partner) => {
        this.saving.set(false);
        this.messages.add({ severity: 'success', summary: '已儲存', detail: `原廠「${partner.name}」已儲存` });
        this.router.navigate(['/partners', partner.pkid]);
      },
      error: () => {
        this.saving.set(false);
        this.messages.add({ severity: 'error', summary: '錯誤', detail: '儲存失敗' });
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/partners']);
  }

  protected isInvalid(control: string): boolean {
    const c = this.form.get(control);
    return !!c && c.invalid && (c.dirty || c.touched);
  }
}
