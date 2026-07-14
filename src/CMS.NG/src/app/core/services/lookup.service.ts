import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { AppUserLookup } from '@core/models/app-role.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';
import { CourseGroupLookup } from '@core/models/course-group.model';

/** Slim lookup lists used to populate form selects. */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/lookups`;

  /** Active application users (for the role users multi-select). */
  appUsers(): Observable<AppUserLookup[]> {
    return this.http.get<AppUserLookup[]>(`${this.baseUrl}/app-users`);
  }

  /** Publishing statuses (for Course/Promotion form selects). */
  publishStatuses(): Observable<PublishStatusLookup[]> {
    return this.http.get<PublishStatusLookup[]>(`${this.baseUrl}/publish-statuses`);
  }

  /** Course groups (for the Course form select). */
  courseGroups(): Observable<CourseGroupLookup[]> {
    return this.http.get<CourseGroupLookup[]>(`${this.baseUrl}/course-groups`);
  }
}
