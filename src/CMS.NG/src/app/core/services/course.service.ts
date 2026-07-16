import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { Course, CourseQuery, CourseRequest } from '@core/models/course.model';

/** Data access for the Course CRUD endpoints. */
@Injectable({ providedIn: 'root' })
export class CourseService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/courses`;

  /** GET all courses. */
  list(): Observable<Course[]> {
    return this.http.get<Course[]>(this.baseUrl);
  }

  /** POST a filtered query. */
  query(filter: CourseQuery): Observable<Course[]> {
    return this.http.post<Course[]>(`${this.baseUrl}/query`, filter);
  }

  /** GET a single course by pkid (numeric PK). */
  get(pkid: number): Observable<Course> {
    return this.http.get<Course>(`${this.baseUrl}/${pkid}`);
  }

  /** POST a new course. */
  create(request: CourseRequest): Observable<Course> {
    return this.http.post<Course>(this.baseUrl, request);
  }

  /** PUT an existing course (pkid carried in the body). */
  update(request: CourseRequest): Observable<Course> {
    return this.http.put<Course>(this.baseUrl, request);
  }

  /** DELETE a course by pkid. */
  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }
}
