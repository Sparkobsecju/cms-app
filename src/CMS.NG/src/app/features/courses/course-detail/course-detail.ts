import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { QrService } from '@core/services/qr.service';
import { Course } from '@core/models/course.model';

/** Read-only detail page for a course (檢視課程). */
@Component({
  selector: 'app-course-detail',
  imports: [CommonModule, ButtonModule, TagModule],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss',
})
export class CourseDetail implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookups = inject(LookupService);
  private readonly qr = inject(QrService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(true);

  /** Public page URL encoded in the QR code (uuu.com.tw). */
  protected readonly qrUrl = computed(() => {
    const c = this.course();
    return c ? `https://www.uuu.com.tw/Course/Show/${c.pkid}/${c.courseId}` : null;
  });

  /** Rendered QR image as a PNG data URL; null until generated. */
  protected readonly qrDataUrl = signal<string | null>(null);

  constructor() {
    // Regenerate the QR image whenever the encoded URL changes.
    effect(() => {
      const url = this.qrUrl();
      if (!url) {
        this.qrDataUrl.set(null);
        return;
      }
      this.qr
        .toDataUrl(url)
        .then((data) => this.qrDataUrl.set(data))
        .catch(() => this.qrDataUrl.set(null));
    });
  }

  private readonly certificationLabels = signal<Map<number, string>>(new Map());
  private readonly jobCategoryLabels = signal<Map<number, string>>(new Map());

  /** Resolved Certification labels for the current course. */
  protected readonly certificationNames = computed(() => {
    const c = this.course();
    const map = this.certificationLabels();
    return c ? c.certificationPkids.map((id) => map.get(id) ?? `#${id}`) : [];
  });

  /** Resolved JobCategory labels for the current course. */
  protected readonly jobCategoryNames = computed(() => {
    const c = this.course();
    const map = this.jobCategoryLabels();
    return c ? c.jobCategoryPkids.map((id) => map.get(id) ?? `#${id}`) : [];
  });

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    forkJoin({
      course: this.service.get(id),
      certifications: this.lookups.certifications(),
      jobCategories: this.lookups.jobCategories(),
    }).subscribe({
      next: ({ course, certifications, jobCategories }) => {
        this.certificationLabels.set(new Map(certifications.map((c) => [c.pkid, `${c.partnerName} - ${c.title}`])));
        this.jobCategoryLabels.set(new Map(jobCategories.map((j) => [j.pkid, j.description])));
        this.course.set(course);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected back(): void {
    this.router.navigate(['/courses']);
  }

  protected edit(): void {
    const course = this.course();
    if (course) {
      this.router.navigate(['/courses', course.pkid, 'edit']);
    }
  }

  /** Download the generated QR image as `{CourseId}.png`. */
  protected downloadQr(): void {
    const data = this.qrDataUrl();
    const c = this.course();
    if (!data || !c) {
      return;
    }
    const link = document.createElement('a');
    link.href = data;
    link.download = `${c.courseId}.png`;
    link.click();
  }
}
