import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';

/** Read-only detail page for a course group (檢視課程群組). */
@Component({
  selector: 'app-course-group-detail',
  imports: [CommonModule, ButtonModule],
  templateUrl: './course-group-detail.html',
  styleUrl: './course-group-detail.scss',
})
export class CourseGroupDetail implements OnInit {
  private readonly service = inject(CourseGroupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly group = signal<CourseGroup | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.get(id).subscribe({
      next: (group) => {
        this.group.set(group);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected back(): void {
    this.router.navigate(['/course-groups']);
  }

  protected edit(): void {
    const group = this.group();
    if (group) {
      this.router.navigate(['/course-groups', group.pkid, 'edit']);
    }
  }
}
