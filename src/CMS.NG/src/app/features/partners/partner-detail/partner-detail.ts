import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';
import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

/** Read-only detail page for a partner (檢視原廠). */
@Component({
  selector: 'app-partner-detail',
  imports: [CommonModule, ButtonModule, RowAuditBadge],
  templateUrl: './partner-detail.html',
  styleUrl: './partner-detail.scss',
})
export class PartnerDetail implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly partner = signal<Partner | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.get(id).subscribe({
      next: (partner) => {
        this.partner.set(partner);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected back(): void {
    this.router.navigate(['/partners']);
  }

  protected edit(): void {
    const partner = this.partner();
    if (partner) {
      this.router.navigate(['/partners', partner.pkid, 'edit']);
    }
  }

  protected viewCourses(): void {
    const partner = this.partner();
    if (partner) {
      this.router.navigate(['/courses'], { queryParams: { partnerPkid: partner.pkid } });
    }
  }
}
