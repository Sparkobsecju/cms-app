import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';
import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

/** Read-only detail page for a publishing status (檢視發布狀態). */
@Component({
  selector: 'app-publish-status-detail',
  imports: [CommonModule, ButtonModule, TagModule, RowAuditBadge],
  templateUrl: './publish-status-detail.html',
  styleUrl: './publish-status-detail.scss',
})
export class PublishStatusDetail implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly status = signal<PublishStatus | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.get(id).subscribe({
      next: (status) => {
        this.status.set(status);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected back(): void {
    this.router.navigate(['/publish-statuses']);
  }

  protected edit(): void {
    const status = this.status();
    if (status) {
      this.router.navigate(['/publish-statuses', status.pkid, 'edit']);
    }
  }
}
