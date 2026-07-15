import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { PublishStatus, PublishStatusQuery, PublishStatusRequest } from '@core/models/publish-status.model';

/** Data access for the PublishStatus CRUD endpoints. */
@Injectable({ providedIn: 'root' })
export class PublishStatusService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/publishstatuses`;

  /** GET all statuses. */
  list(): Observable<PublishStatus[]> {
    return this.http.get<PublishStatus[]>(this.baseUrl);
  }

  /** POST a filtered query. */
  query(filter: PublishStatusQuery): Observable<PublishStatus[]> {
    return this.http.post<PublishStatus[]>(`${this.baseUrl}/query`, filter);
  }

  /** GET a single status by pkid (numeric PK). */
  get(pkid: number): Observable<PublishStatus> {
    return this.http.get<PublishStatus>(`${this.baseUrl}/${pkid}`);
  }

  /** POST a new status. */
  create(request: PublishStatusRequest): Observable<PublishStatus> {
    return this.http.post<PublishStatus>(this.baseUrl, request);
  }

  /** PUT an existing status (pkid carried in the body). */
  update(request: PublishStatusRequest): Observable<PublishStatus> {
    return this.http.put<PublishStatus>(this.baseUrl, request);
  }

  /** DELETE a status by pkid. */
  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }
}
