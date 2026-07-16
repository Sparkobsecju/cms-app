import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { AppUserLookup } from '@core/models/app-role.model';
import { AppRoleLookup } from '@core/models/app-user.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import { CertificationLookup, JobCategoryLookup, PartnerLookup } from '@core/models/course.model';
import { PromotionLookup, TrainingCenterLookup } from '@core/models/featured-promo-item.model';

/** Slim lookup lists used to populate form selects. */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/lookups`;

  /** Active application users (for the role users multi-select). */
  appUsers(): Observable<AppUserLookup[]> {
    return this.http.get<AppUserLookup[]>(`${this.baseUrl}/app-users`);
  }

  /** Application roles (for the user roles multi-select). */
  appRoles(): Observable<AppRoleLookup[]> {
    return this.http.get<AppRoleLookup[]>(`${this.baseUrl}/app-roles`);
  }

  /** Publishing statuses (for Course/Promotion form selects). */
  publishStatuses(): Observable<PublishStatusLookup[]> {
    return this.http.get<PublishStatusLookup[]>(`${this.baseUrl}/publish-statuses`);
  }

  /** Course groups (for the Course form select). */
  courseGroups(): Observable<CourseGroupLookup[]> {
    return this.http.get<CourseGroupLookup[]>(`${this.baseUrl}/course-groups`);
  }

  /** Partners (for the Course form select). */
  partners(): Observable<PartnerLookup[]> {
    return this.http.get<PartnerLookup[]>(`${this.baseUrl}/partners`);
  }

  /** Certifications (for the Course form multi-select). */
  certifications(): Observable<CertificationLookup[]> {
    return this.http.get<CertificationLookup[]>(`${this.baseUrl}/certifications`);
  }

  /** Job categories (for the Course form multi-select). */
  jobCategories(): Observable<JobCategoryLookup[]> {
    return this.http.get<JobCategoryLookup[]>(`${this.baseUrl}/job-categories`);
  }

  /** Training centers (for the FeaturedPromoItem tabs / filter). */
  trainingCenters(): Observable<TrainingCenterLookup[]> {
    return this.http.get<TrainingCenterLookup[]>(`${this.baseUrl}/training-centers`);
  }

  /** Promotions (for the FeaturedPromoItem PromoCode lookup). */
  promotions(): Observable<PromotionLookup[]> {
    return this.http.get<PromotionLookup[]>(`${this.baseUrl}/promotions`);
  }
}
