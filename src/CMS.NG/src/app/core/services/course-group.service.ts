import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { CourseGroup, CourseGroupQuery, CourseGroupRequest } from '@core/models/course-group.model';

/** Data access for the CourseGroup CRUD endpoints. */
@Injectable({ providedIn: 'root' })
export class CourseGroupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/coursegroups`;

  /** GET all course groups. */
  list(): Observable<CourseGroup[]> {
    return this.http.get<CourseGroup[]>(this.baseUrl);
  }

  /** POST a filtered query. */
  query(filter: CourseGroupQuery): Observable<CourseGroup[]> {
    return this.http.post<CourseGroup[]>(`${this.baseUrl}/query`, filter);
  }

  /** GET a single course group by pkid (numeric PK). */
  get(pkid: number): Observable<CourseGroup> {
    return this.http.get<CourseGroup>(`${this.baseUrl}/${pkid}`);
  }

  /** POST a new course group. */
  create(request: CourseGroupRequest): Observable<CourseGroup> {
    return this.http.post<CourseGroup>(this.baseUrl, request);
  }

  /** PUT an existing course group (pkid carried in the body). */
  update(request: CourseGroupRequest): Observable<CourseGroup> {
    return this.http.put<CourseGroup>(this.baseUrl, request);
  }

  /** DELETE a course group by pkid. */
  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }
}
