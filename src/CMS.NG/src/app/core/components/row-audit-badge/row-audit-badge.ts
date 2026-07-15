import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DialogModule } from 'primeng/dialog';
import { RowAuditService } from '@core/services/row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

/**
 * Reusable toolbar badge showing a record's Row Audit history (異動紀錄 History).
 * Inline it shows the most recent change; clicking opens a dialog with the full trail.
 * Drop into any detail/form toolbar with the page's tableName and the record's pkid.
 */
@Component({
  selector: 'app-row-audit-badge',
  imports: [CommonModule, DialogModule],
  templateUrl: './row-audit-badge.html',
  styleUrl: './row-audit-badge.scss',
})
export class RowAuditBadge {
  private readonly service = inject(RowAuditService);

  /** Business table name (e.g. "Course"). */
  readonly tableName = input.required<string>();
  /** The record's numeric pkid. */
  readonly pkid = input.required<number>();

  protected readonly history = signal<RowAuditEntry[]>([]);
  protected readonly loading = signal(true);
  protected readonly dialogVisible = signal(false);

  /** Most recent entry (the endpoint returns rows newest first). */
  protected readonly latest = computed(() => this.history()[0] ?? null);

  constructor() {
    // (Re)fetch whenever the target record changes.
    effect(() => {
      const tableName = this.tableName();
      const pkid = this.pkid();
      if (!tableName || !pkid) {
        this.history.set([]);
        this.loading.set(false);
        return;
      }
      this.loading.set(true);
      this.service.history(tableName, pkid).subscribe({
        next: (rows) => {
          this.history.set(rows);
          this.loading.set(false);
        },
        error: () => {
          this.history.set([]);
          this.loading.set(false);
        },
      });
    });
  }

  protected open(): void {
    this.dialogVisible.set(true);
  }
}
