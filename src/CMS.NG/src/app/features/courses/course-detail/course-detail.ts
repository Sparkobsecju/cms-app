import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
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
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(true);

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
}
